# One-shot CPU-only cover OCR; no HTTP service, no custom remote code.
import json, os, sys, time, resource
from pathlib import Path
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["HF_HUB_DISABLE_XET"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"
os.environ["OMP_NUM_THREADS"] = "2"
os.environ["MKL_NUM_THREADS"] = "2"
RUNTIME_PROTOCOL = "guidevault-vision-deep-v7.1.3"

def dispatch(args, recognize_page):
    if not args:
        raise ValueError("A page path or batch option is required.")
    if args[0] in ("--batch", "--deep-batch"):
        if len(args) < 2:
            raise ValueError("Batch requires at least one page path.")
        results = []
        for path in args[1:]:
            try:
                results.append(recognize_page(path, deep=args[0] == "--deep-batch"))
            except Exception as exc:
                results.append({"error": str(exc)[:500]})
        return results
    if args[0].startswith("--") or len(args) != 1:
        raise ValueError("Unknown option or invalid page arguments.")
    return recognize_page(args[0])

# Check the actual CLI routing without loading PyTorch or downloading weights.
if sys.argv[1:] == ["--self-test"]:
    def probe(path, deep=False):
        return {"path": path, "deep": deep}
    if dispatch(["--deep-batch", "a", "b"], probe) != [{"path":"a","deep":True},{"path":"b","deep":True}]:
        raise RuntimeError("Deep batch routing failed")
    if dispatch(["--batch", "a"], probe) != [{"path":"a","deep":False}]:
        raise RuntimeError("Batch routing failed")
    if dispatch(["a"], probe) != {"path":"a","deep":False}:
        raise RuntimeError("Single page routing failed")
    print(RUNTIME_PROTOCOL)
    sys.exit(0)

import torch
from PIL import Image
from transformers import AutoProcessor, Florence2ForConditionalGeneration

MODEL = "florence-community/Florence-2-base"
REVISION = "00921df"
torch.set_num_threads(2)
torch.set_num_interop_threads(1)
started = time.monotonic()
cache = Path(os.environ.get("GUIDEVAULT_DATA", "/data")) / "vision-models" / "florence2"
cache.mkdir(parents=True, exist_ok=True)
processor = AutoProcessor.from_pretrained(MODEL, revision=REVISION, cache_dir=cache, trust_remote_code=False)
model = Florence2ForConditionalGeneration.from_pretrained(MODEL, revision=REVISION, cache_dir=cache, use_safetensors=True, dtype=torch.float32).eval()
# Bounded adaptive passes. Keep the model's existing tested token ceiling.
def retry_depth():
    try:
        return max(0, min(2, int(os.environ.get("GUIDEVAULT_VISION_RETRY_DEPTH", "1"))))
    except ValueError:
        return 1

def overlap(a, b):
    iw = max(0, min(a["x"]+a["width"], b["x"]+b["width"])-max(a["x"], b["x"]))
    ih = max(0, min(a["y"]+a["height"], b["y"]+b["height"])-max(a["y"], b["y"]))
    return iw*ih

def norm(text):
    return "".join(c.lower() for c in text if c.isalnum())

def reconcile(regions):
    # Prefer interior, high-detail observations over overview or clipped crop edges.
    # Only replace conflicts with nearly identical geometry; do not merge adjacent columns.
    # Keep competing raw observations in diagnostics, not in searchable page text.
    selected, alternatives = [], []
    ordered = sorted(regions, key=lambda r: (r["edge"], -r["detail"], -len(norm(r["text"])) , r["y"], r["x"]))
    for r in ordered:
        if not norm(r["text"]):
            continue
        conflict = None
        for p in selected:
            area = overlap(r,p)
            ra, pa = r["width"]*r["height"], p["width"]*p["height"]
            same = norm(r["text"]) == norm(p["text"])
            # Exact duplicate can have a slightly different box; disagreement requires mutual overlap.
            if same and area >= .55*min(ra,pa):
                conflict = p; break
            if area >= .70*max(ra,pa):
                conflict = p; break
            # Suppress clipped fragments only when they are physically contained and textual substrings.
            if r["edge"] and area >= .85*ra and norm(r["text"]) in norm(p["text"]) and len(norm(r["text"])) < len(norm(p["text"])):
                conflict = p; break
        if conflict is None:
            selected.append(r)
        elif norm(r["text"]) != norm(conflict["text"]):
            alternatives.append(dict(text=r["text"], chosen=conflict["text"], x=r["x"], y=r["y"], width=r["width"], height=r["height"], reason="Overlapping recognition; preferred interior/detail evidence"))
    return sorted(selected,key=lambda r:(r["y"],r["x"])), alternatives

def recognize(path, deep=False):
    page_started = time.monotonic()
    with Image.open(path) as source:
        original = source.convert("RGB")
    w,h = original.size
    image=original.copy(); image.thumbnail((1800,1800))
    observations=[]
    passes=0; retries=0; recovered=0; unresolved=0; hits=0
    depth_limit=retry_depth() if deep else 0
    # Four initial tiles plus at most 24 additional tile passes per page.
    budget=29
    def scan(box, detail):
        nonlocal passes,hits
        x0,y0,x1,y1=box
        tile=original.crop(box); tile.thumbnail((1800,1800))
        found,cut=scan_region(tile,1024 if deep else 768)
        passes+=1; hits+=int(cut)
        for r in found:
            # Mark only artificial crop boundaries, not physical page edges.
            margin=max(3,round(min(tile.size)*.012))
            edge=(x0>0 and r["x"]<=margin or y0>0 and r["y"]<=margin or x1<w and r["x"]+r["width"]>=tile.width-margin or y1<h and r["y"]+r["height"]>=tile.height-margin)
            x=round((x0+r["x"]*(x1-x0)/tile.width)*image.width/w)
            y=round((y0+r["y"]*(y1-y0)/tile.height)*image.height/h)
            rw=max(1,round(r["width"]*(x1-x0)/tile.width*image.width/w))
            rh=max(1,round(r["height"]*(y1-y0)/tile.height*image.height/h))
            if x<image.width and y<image.height:
                observations.append(dict(text=r["text"],x=x,y=y,width=min(rw,image.width-x),height=min(rh,image.height-y),detail=detail,edge=bool(edge)))
        return cut
    def resolve(box, depth, reserve):
        nonlocal retries,recovered,unresolved
        cut=scan(box,depth+1)
        if not cut: return True
        x0,y0,x1,y1=box; bw=x1-x0; bh=y1-y0
        if depth>=depth_limit or min(bw,bh)<160 or passes+4+reserve>budget:
            unresolved+=1; return False
        # Overlapping quadrants preserve text across crop boundaries.
        children=[(x0+round(l*bw),y0+round(t*bh),x0+round(r*bw),y0+round(b*bh)) for l,t,r,b in TILES]
        complete=True
        for i,child in enumerate(children):
            retries+=1
            complete=resolve(child,depth+1,reserve+3-i) and complete
        if complete: recovered+=1
        return complete
    overview_cut=scan((0,0,w,h),0)
    if deep:
        complete=True
        for i,(l,t,r,b) in enumerate(TILES):
            complete=resolve((round(l*w),round(t*h),round(r*w),round(b*h)),0,3-i) and complete
        if overview_cut and complete: recovered+=1
    elif overview_cut: unresolved+=1
    regions,alternatives=reconcile(observations)
    return dict(width=image.width,height=image.height,regions=regions[:800],alternatives=alternatives[:800],seconds=round(time.monotonic()-page_started,2),peakMb=round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss/1024,1),passes=passes,retries=retries,limitHits=hits,recovered=recovered,unresolved=unresolved,truncated=unresolved>0 or len(regions)>800,retryDepth=depth_limit)

TILES=[(0,0,.58,.58),(.42,0,1,.58),(0,.42,.58,1),(.42,.42,1,1)]

def scan_region(image, limit):
    task = "<OCR_WITH_REGION>"
    inputs = processor(text=task, images=image, return_tensors="pt")
    with torch.inference_mode():
        ids = model.generate(**inputs, max_new_tokens=limit, num_beams=1, do_sample=False)
    raw = processor.batch_decode(ids, skip_special_tokens=False)[0]
    parsed = processor.post_process_generation(raw, task=task, image_size=image.size)[task]
    regions = []
    for label, box in zip(parsed["labels"], parsed["quad_boxes"]):
        if len(box) != 8 or not str(label).strip():
            continue
        x = max(0, min(image.width, min(box[::2])))
        y = max(0, min(image.height, min(box[1::2])))
        right = max(0, min(image.width, max(box[::2])))
        bottom = max(0, min(image.height, max(box[1::2])))
        if right > x and bottom > y:
            regions.append(dict(text=str(label)[:500], x=round(x), y=round(y), width=round(right-x), height=round(bottom-y)))
    # Forced EOS at the cap may conceal truncation. Count generated tokens,
    # excluding the single decoder start token, and conservatively flag the cap.
    return regions, ids.shape[-1] - 1 >= limit

print(json.dumps(dispatch(sys.argv[1:], recognize)))

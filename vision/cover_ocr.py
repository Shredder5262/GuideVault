# One-shot CPU-only cover OCR; no HTTP service, no custom remote code.
import json, os, sys, time, resource
from pathlib import Path
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
os.environ["HF_HUB_DISABLE_XET"] = "1"
os.environ["TOKENIZERS_PARALLELISM"] = "false"
os.environ["OMP_NUM_THREADS"] = "2"
os.environ["MKL_NUM_THREADS"] = "2"
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
def recognize(path, deep=False):
    page_started = time.monotonic()
    with Image.open(path) as original:
        image = original.convert("RGB")
    original = image
    image = original.copy()
    image.thumbnail((1800, 1800))
    regions, truncated = scan_region(image, 768)
    passes = 1
    if deep:
        # Four overlapping tiles from the original raster, not the resized overview.
        w, h = original.size
        for left, top, right, bottom in [(0,0,.58,.58),(.42,0,1,.58),(0,.42,.58,1),(.42,.42,1,1)]:
            x0,y0,x1,y1 = round(left*w),round(top*h),round(right*w),round(bottom*h)
            tile = original.crop((x0,y0,x1,y1))
            tile.thumbnail((1800,1800))
            found, cut = scan_region(tile, 1024)
            passes += 1
            truncated = truncated or cut
            for r in found:
                x = round((x0+r["x"]*(x1-x0)/tile.width)*image.width/w)
                y = round((y0+r["y"]*(y1-y0)/tile.height)*image.height/h)
                rw = max(1,round(r["width"]*(x1-x0)/tile.width*image.width/w))
                rh = max(1,round(r["height"]*(y1-y0)/tile.height*image.height/h))
                candidate = dict(text=r["text"],x=x,y=y,width=min(rw,image.width-x),height=min(rh,image.height-y))
                if candidate["width"]>0 and candidate["height"]>0 and not any(same_region(candidate, prior) for prior in regions):
                    regions.append(candidate)
    return dict(width=image.width,height=image.height,regions=regions[:800],seconds=round(time.monotonic()-page_started,2),peakMb=round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss/1024,1),passes=passes,truncated=truncated or len(regions)>800)

def same_region(a,b):
    norm = lambda text: "".join(c.lower() for c in text if c.isalnum())
    if norm(a["text"]) != norm(b["text"]): return False
    iw=max(0,min(a["x"]+a["width"],b["x"]+b["width"])-max(a["x"],b["x"]))
    ih=max(0,min(a["y"]+a["height"],b["y"]+b["height"])-max(a["y"],b["y"]))
    return iw*ih >= .55*min(a["width"]*a["height"],b["width"]*b["height"])

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
    return regions, ids.shape[-1] >= limit

if len(sys.argv) > 2 and sys.argv[1] in ("--batch", "--deep-batch"):
    results = []
    for path in sys.argv[2:]:
        try:
            results.append(recognize(path, deep=sys.argv[1] == "--deep-batch"))
        except Exception as exc:
            results.append({"error": str(exc)[:500]})
    print(json.dumps(results))
else:
    print(json.dumps(recognize(sys.argv[1])))

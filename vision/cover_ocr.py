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
with Image.open(sys.argv[1]) as original:
    image = original.convert("RGB")
image.thumbnail((1800, 1800))
task = "<OCR_WITH_REGION>"
inputs = processor(text=task, images=image, return_tensors="pt")
with torch.inference_mode():
    ids = model.generate(**inputs, max_new_tokens=768, num_beams=1, do_sample=False)
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
print(json.dumps(dict(width=image.width, height=image.height, regions=regions[:200], seconds=round(time.monotonic()-started,2), peakMb=round(resource.getrusage(resource.RUSAGE_SELF).ru_maxrss/1024,1))))

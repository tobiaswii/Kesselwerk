import sys, os, json; sys.path.insert(0, "/home/claude/creatures")
from species import *
from roster import ROSTER, model_name
os.makedirs("/home/claude/creatures/out/models", exist_ok=True)
os.makedirs("/home/claude/creatures/out/prev", exist_ok=True)
only = sys.argv[1:]
for c in ROSTER:
    if only and c["id"] not in only: continue
    try:
        build(c["model"])
        preview(f"/home/claude/creatures/out/prev/{c['id']}.png", size=300, samples=16, action="Idle", frame=0)
        export_fbx(f"/home/claude/creatures/out/models/{model_name(c)}.fbx")
        print("OK", c["id"], flush=True)
    except Exception as e:
        import traceback; traceback.print_exc()
        print("FEHLER", c["id"], e, flush=True)

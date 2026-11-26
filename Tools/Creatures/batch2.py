import sys, os; sys.path.insert(0, "/home/claude/creatures")
from species import *
from roster import REPLACE
os.makedirs("/home/claude/creatures/out/models2", exist_ok=True)
only = sys.argv[1:]
for cid, (model, spec) in REPLACE.items():
    if only and cid not in only: continue
    try:
        spec = dict(spec, id=cid)
        build(spec)
        preview(f"/home/claude/creatures/out/prev/{cid}.png", size=300, samples=16, action="Idle", frame=0)
        export_fbx(f"/home/claude/creatures/out/models2/{model}.fbx")
        print("OK", cid, flush=True)
    except Exception as e:
        import traceback; traceback.print_exc(); print("FEHLER", cid, e, flush=True)

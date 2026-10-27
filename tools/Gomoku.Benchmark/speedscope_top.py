"""Summarise a speedscope JSON (dotnet-trace --format Speedscope): self and inclusive time per frame."""
import json, sys, collections

data = json.load(open(sys.argv[1]))
frames = [f["name"] for f in data["shared"]["frames"]]
PSEUDO = {'CPU_TIME', 'UNMANAGED_CODE_TIME'}
self_t = collections.Counter()
incl_t = collections.Counter()
total = 0.0

for prof in data["profiles"]:
    if prof["type"] == "evented":
        stack = []  # (frame, start)
        last = None
        for ev in prof["events"]:
            t = ev["at"]
            if stack and last is not None:
                top = [g for g, _ in stack if frames[g] not in PSEUDO]
                if top and frames[stack[-1][0]] == "CPU_TIME":
                    self_t[top[-1]] += t - last
                    total += t - last
            last = t
            if ev["type"] == "O":
                stack.append((ev["frame"], t))
            else:
                f, start = stack.pop()
                if all(f != g for g, _ in stack):  # count recursion once
                    incl_t[f] += t - start
    else:  # sampled
        for sample, w in zip(prof["samples"], prof["weights"]):
            total += w
            if sample:
                self_t[sample[-1]] += w
            for f in set(sample):
                incl_t[f] += w

def short(name):
    return name.replace("Gomoku.Core.", "").split("(")[0][:110]

n = int(sys.argv[2]) if len(sys.argv) > 2 else 25
print(f"total {total:.0f}")
print("SELF")
for f, t in self_t.most_common(n):
    print(f"{100*t/total:6.2f}%  {short(frames[f])}")
print("INCLUSIVE")
for f, t in incl_t.most_common(n):
    print(f"{100*t/total:6.2f}%  {short(frames[f])}")

import re
from pathlib import Path

ROOT = Path(r"d:\Project\Thereasm")

def inspect_scene(scene_path):
    print(f"=== Inspecting {scene_path.name} ===")
    content = scene_path.read_text(encoding="utf-8-sig")
    
    # Map file_id to (type_id, body)
    blocks = re.findall(r"(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=\n--- !u!|\Z)", content, re.DOTALL)
    records = {int(b[1]): (int(b[0]), b[2]) for b in blocks}
    
    def get_field(body, key):
        m = re.search(rf"(?m)^\s*{re.escape(key)}:\s*(.*)$", body)
        return m.group(1) if m else None
    
    def get_components(go_id):
        body = records[go_id][1]
        return [int(x) for x in re.findall(r"component: \{fileID: (-?\d+)\}", body)]
    
    def get_children(rt_id):
        body = records[rt_id][1]
        m = re.search(r"m_Children:(.*?)(?:m_Father|\Z)", body, re.DOTALL)
        if not m:
            return []
        return [int(x) for x in re.findall(r"- \{fileID: (-?\d+)\}", m.group(1))]

    # Recursive dump from a RectTransform
    def dump_tree(rt_id, indent=0):
        t, b = records[rt_id]
        go_id = int(re.search(r"m_GameObject: \{fileID: (-?\d+)\}", b).group(1))
        go_name = get_field(records[go_id][1], "m_Name")
        comps = get_components(go_id)
        comp_names = []
        for c in comps:
            ct, cb = records[c]
            if ct == 114:
                m_script = get_field(cb, "m_Script")
                script_guid = re.search(r"guid: (\w+)", m_script)
                guid_val = script_guid.group(1) if script_guid else "unknown"
                # Check for Image, Button, NoteBlock, NoteSlot, Text, etc.
                if guid_val == "fe87c0e1cc204ed48ad3b37840f39efc":
                    sprite = get_field(cb, "m_Sprite")
                    color = get_field(cb, "m_Color")
                    comp_names.append(f"Image(sprite={sprite}, color={color})")
                elif guid_val == "4e29b1a8efbd4b44bb3f3716e73f07ff":
                    comp_names.append("Button")
                elif guid_val == "f4688fdb7df04437aeb418b961361dc5":
                    text = get_field(cb, "m_text")
                    comp_names.append(f"TMP_Text('{text}')")
                elif guid_val == "5f7201a12d95ffc409449d95f23cf332":
                    text = get_field(cb, "m_Text")
                    comp_names.append(f"UI_Text('{text}')")
                else:
                    comp_names.append(f"MonoBehaviour({guid_val})")
            elif ct == 224:
                comp_names.append("RectTransform")
            elif ct == 222:
                comp_names.append("CanvasRenderer")
            elif ct == 225:
                comp_names.append("CanvasGroup")
            elif ct == 114:
                pass
            else:
                comp_names.append(f"Type_{ct}")

        pos = get_field(b, "m_AnchoredPosition")
        size = get_field(b, "m_SizeDelta")
        anch_min = get_field(b, "m_AnchorMin")
        anch_max = get_field(b, "m_AnchorMax")
        print("  " * indent + f"- {go_name} (GO:{go_id}, RT:{rt_id}) [{pos}, size={size}, ancMin={anch_min}, ancMax={anch_max}] Comps: {', '.join(comp_names)}")
        for ch in get_children(rt_id):
            dump_tree(ch, indent + 1)

    print("=== DUMPING NotePanel ===")
    dump_tree(1927024419)


inspect_scene(ROOT / "Assets/Scenes/MainBoard.unity")

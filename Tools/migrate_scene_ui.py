"""Migrate scene UI hierarchy to safe frame structure using serialized YAML manipulation."""
from pathlib import Path
import json
import math
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "Assets/Resources/ThereasmUI"
THEME = ROOT / "Assets/UI/Themes/ThereasmTheme.asset"
GOLD = (247/255, 212/255, 153/255, 1)
PAPER = (248/255, 236/255, 213/255, 1)
MUTED = (202/255, 181/255, 164/255, 1)


def guid(path):
    path = ROOT / path
    return re.search(r"^guid: (\w+)", path.with_name(path.name + ".meta").read_text(), re.M)[1]


def ensure_meta(path):
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n", encoding="utf-8")


ensure_meta(THEME)
for name in ("GameStartUI", "GameUITheme", "GameBoardLayout"):
    ensure_meta(ROOT / f"Assets/Scripts/{name}.cs")

UGUI = "Library/PackageCache/com.unity.ugui/Runtime/UGUI/UI/Core/"
SCRIPTS = {name: guid(UGUI + name + ".cs") for name in ("Image", "Text", "Button", "GraphicRaycaster", "ScrollRect", "RectMask2D")}
SCRIPTS["CanvasScaler"] = guid(UGUI + "Layout/CanvasScaler.cs")
SCRIPTS["ContentSizeFitter"] = guid(UGUI + "Layout/ContentSizeFitter.cs")
SCRIPTS["AspectRatioFitter"] = guid(UGUI + "Layout/AspectRatioFitter.cs")
for name in ("GameStartUI", "GameBoardLayout", "GameUITheme", "CustomCusor", "TargetingArrow", "HoldToReveal"):
    SCRIPTS[name] = guid(f"Assets/Scripts/{name}.cs")


def ref(value): return f"{{fileID: {value}}}"
def sprite(name):
    meta = (ART / (name + '.png.meta')).read_text()
    mode = int(re.search(r'^  spriteMode: (\d+)', meta, re.M)[1])
    file_id = re.search(r'internalID: (-?\d+)', meta)[1] if mode == 2 else '21300000'
    return f"{{fileID: {file_id}, guid: {guid('Assets/Resources/ThereasmUI/' + name + '.png')}, type: 3}}"
def font(path): return f"{{fileID: 12800000, guid: {guid(path)}, type: 3}}"
def color(c): return "{" + ", ".join(f"{k}: {v:g}" for k, v in zip("rgba", c)) + "}"
def scalar(text): return json.dumps(text, ensure_ascii=False)
def vector(v): return "{" + ", ".join(f"{k}: {value:g}" for k, value in zip("xyzw", v)) + "}"


class Scene:
    def __init__(self, path, allow_migrated=False):
        self.path = ROOT / path
        source = self.path.read_text(encoding="utf-8-sig")
        if not allow_migrated and ('m_Name: "Thereasm UI"' in source or "m_Name: Thereasm UI\n" in source):
            raise RuntimeError(f"Already migrated: {path}. Preserve manual edits; do not regenerate.")
        matches = list(re.finditer(r"(?m)^--- !u!(\d+) &(-?\d+)(?: stripped)?\n", source))
        self.header = source[:matches[0].start()]
        self.records = {}
        self.types = {}
        self.stripped = {}
        for i, match in enumerate(matches):
            id_ = int(match[2])
            self.types[id_] = int(match[1])
            self.stripped[id_] = ' stripped' in match[0]
            self.records[id_] = source[match.end():matches[i+1].start() if i+1 < len(matches) else len(source)]
        self.next_id = 7000000000000000000
        self.panels, self.buttons = [], []
        self.manifest = []

    def alloc(self):
        self.next_id += 1
        while self.next_id in self.records: self.next_id += 1
        return self.next_id

    def add(self, type_, body, id_=None):
        id_ = id_ or self.alloc()
        self.types[id_] = type_
        self.records[id_] = body.rstrip() + "\n"
        return id_

    def set(self, id_, name, value):
        record = self.records[id_]
        pattern = rf"(?m)^  {re.escape(name)}:.*$"
        if re.search(pattern, record): record = re.sub(pattern, lambda m: f"  {name}: {value}", record, count=1)
        else: record += f"  {name}: {value}\n"
        self.records[id_] = record

    def value(self, id_, name):
        return re.search(rf"(?m)^  {re.escape(name)}: (.*)$", self.records[id_])[1]

    def children(self, rect):
        match = re.search(r"  m_Children:(.*?)(?=  m_Father:)", self.records[rect], re.S)
        return list(map(int, re.findall(r"fileID: (-?\d+)", match[1]))) if match else []

    def set_children(self, rect, children):
        block = "  m_Children:" + ("\n" + "".join(f"  - {ref(c)}\n" for c in children) if children else " []\n")
        self.records[rect] = re.sub(r"  m_Children:.*?(?=  m_Father:)", lambda m: block, self.records[rect], flags=re.S)

    def game_object(self, component): return int(re.search(r"m_GameObject: \{fileID: (-?\d+)\}", self.records[component])[1])

    def component(self, go, *, type_=None, script=None):
        body = re.search(r"m_Component:(.*?)(?=  m_Layer:)", self.records[go], re.S)[1]
        for item in map(int, re.findall(r"fileID: (-?\d+)", body)):
            if type_ is not None and self.types.get(item) == type_: return item
            if script and f"guid: {script}," in self.records.get(item, ""): return item
        return None

    def find_script(self, script):
        return next((id_ for id_, body in self.records.items() if f"guid: {script}," in body), None)

    def parent(self, rect, parent):
        old = int(re.search(r"m_Father: \{fileID: (-?\d+)\}", self.records[rect])[1])
        if old: self.set_children(old, [c for c in self.children(old) if c != rect])
        if parent:
            kids = self.children(parent)
            if rect not in kids: self.set_children(parent, kids + [rect])
        self.set(rect, "m_Father", ref(parent))
        roots_id = 9223372036854775807
        if roots_id in self.records:
            self.records[roots_id] = re.sub(rf"(?m)^  - \{{fileID: {rect}\}}\n", "", self.records[roots_id])

    def components(self, go, ids):
        block = "  m_Component:\n" + "".join(f"  - component: {ref(c)}\n" for c in ids)
        self.records[go] = re.sub(r"  m_Component:.*?(?=  m_Layer:)", lambda m: block, self.records[go], flags=re.S)

    def attach(self, go, component):
        ids = list(map(int, re.findall(r"component: \{fileID: (-?\d+)\}", self.records[go])))
        self.components(go, ids + [component])

    def node(self, name, parent=0, rect=(0, 0, 1600, 900), active=True):
        go, transform = self.alloc(), self.alloc()
        self.add(1, f"""GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {ref(transform)}
  m_Layer: 5
  m_Name: {scalar(name)}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: {int(active)}
""", go)
        x, y, w, h = rect
        self.add(224, f"""RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {ref(go)}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: {x}, y: {-y}}}
  m_SizeDelta: {{x: {w}, y: {h}}}
  m_Pivot: {{x: 0, y: 1}}
""", transform)
        if parent: self.parent(transform, parent)
        else: self.records[9223372036854775807] += f"  - {ref(transform)}\n"
        self.manifest.append(dict(name=name, go=go, rect=transform, parent=parent, bounds=list(rect)))
        return go, transform

    def mono(self, go, script, fields="", id_=None):
        id_ = self.add(114, f"""MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {ref(go)}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS[script]}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
{fields}
""", id_)
        self.attach(go, id_)
        return id_

    def renderer(self, go):
        id_ = self.add(222, f"CanvasRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {ref(go)}\n  m_CullTransparentMesh: 1\n")
        self.attach(go, id_)

    def image(self, go, art=None, tint=(1,1,1,1), sliced=False, ppu=8, raycast=False):
        self.renderer(go)
        return self.mono(go, "Image", f"""  m_Material: {{fileID: 0}}
  m_Color: {color(tint)}
  m_RaycastTarget: {int(raycast)}
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {art or '{fileID: 0}'}
  m_Type: {1 if sliced else 0}
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: {ppu}
""")

    def panel(self, name, parent, rect, active=True):
        go, tr = self.node(name, parent, rect, active)
        self.panels.append(self.image(go, sprite("painted-panel"), sliced=True, raycast=True))
        return go, tr

    def label(self, name, parent, rect, value, size=19, tint=PAPER, title=False, center=False):
        go, tr = self.node(name, parent, rect)
        self.renderer(go)
        text = self.mono(go, "Text", f"""  m_Material: {{fileID: 0}}
  m_Color: {color(tint)}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_FontData:
    m_Font: {font('Assets/Almendra-Regular.ttf' if title else 'Assets/TextMesh Pro/Fonts/LiberationSans.ttf')}
    m_FontSize: {size}
    m_FontStyle: 0
    m_BestFit: 0
    m_MinSize: 10
    m_MaxSize: {size}
    m_Alignment: {4 if center else 0}
    m_AlignByGeometry: 0
    m_RichText: 1
    m_HorizontalOverflow: 0
    m_VerticalOverflow: 0
    m_LineSpacing: 1
  m_Text: {scalar(value)}
""")
        return text, tr

    def button(self, name, parent, rect, text, target, method, cls="GameStartUI", argument=None):
        go, tr = self.node(name, parent, rect)
        image = self.image(go, sprite("painted-panel"), sliced=True, ppu=10, raycast=True)
        self.buttons.append(image)
        button = self.mono(go, "Button", f"""  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {{fileID: 0}}
    m_SelectOnDown: {{fileID: 0}}
    m_SelectOnLeft: {{fileID: 0}}
    m_SelectOnRight: {{fileID: 0}}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {{r: 1, g: 1, b: 1, a: 1}}
    m_HighlightedColor: {{r: 1, g: 0.86, b: 0.64, a: 1}}
    m_PressedColor: {{r: 0.7, g: 0.56, b: 0.42, a: 1}}
    m_SelectedColor: {{r: 1, g: 0.86, b: 0.64, a: 1}}
    m_DisabledColor: {{r: 0.55, g: 0.5, b: 0.47, a: 0.65}}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {{fileID: 0}}
    m_PressedSprite: {{fileID: 0}}
    m_SelectedSprite: {{fileID: 0}}
    m_DisabledSprite: {{fileID: 0}}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {ref(image)}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {ref(target)}
        m_TargetAssemblyTypeName: {cls}, Assembly-CSharp
        m_MethodName: {method}
        m_Mode: {3 if argument is not None else 1}
        m_Arguments:
          m_ObjectArgument: {{fileID: 0}}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: {argument or 0}
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
""")
        label, _ = self.label(name + " Label", tr, (12,0,rect[2]-24,rect[3]), text, center=True)
        return go, tr, button, label

    def canvas(self, go, sorting=100):
        template_id = next(id_ for id_, ty in self.types.items() if ty == 223)
        body = self.records[template_id]
        body = re.sub(r"m_GameObject: \{fileID: .*?\}", f"m_GameObject: {ref(go)}", body)
        body = re.sub(r"(?m)^  m_SortingOrder: .*", f"  m_SortingOrder: {sorting}", body)
        body = re.sub(r"(?m)^  m_Enabled: .*", "  m_Enabled: 1", body)
        body = re.sub(r"(?m)^  m_OverrideSorting: .*", "  m_OverrideSorting: 1", body)
        body = re.sub(r"(?m)^  m_Camera: .*", "  m_Camera: {fileID: 0}", body)
        id_ = self.add(223, body)
        self.attach(go, id_)
        return id_

    def position(self, rect, bounds, center=False, scale=1):
        x,y,w,h = bounds
        anchor = (.5,.5) if center else (0,1)
        for key in ("m_AnchorMin", "m_AnchorMax", "m_Pivot"): self.set(rect, key, vector(anchor))
        self.set(rect,"m_AnchoredPosition",vector((x, y if center else -y)))
        self.set(rect,"m_SizeDelta",vector((w,h)))
        self.set(rect,"m_LocalScale",vector((scale,scale,1)))

    def stretch(self, rect):
        self.set(rect,"m_AnchorMin",vector((0,0)))
        self.set(rect,"m_AnchorMax",vector((1,1)))
        self.set(rect,"m_Pivot",vector((.5,.5)))
        self.set(rect,"m_SizeDelta",vector((0,0)))
        self.set(rect,"m_AnchoredPosition",vector((0,0)))

    def move(self, go, parent, bounds, *, center=False, scale=1):
        rect = self.component(go,type_=224)
        self.parent(rect,parent)
        self.position(rect,bounds,center,scale)
        return rect

    def move_control(self, go, parent, bounds, text):
        tr = self.move(go,parent,bounds)
        for child in self.children(tr): self.set(self.game_object(child),"m_IsActive",0)
        image = self.component(go,script=SCRIPTS["Image"])
        self.set(image,"m_Sprite",sprite("painted-panel"))
        self.set(image,"m_Type",1)
        self.set(image,"m_Color",color((1,1,1,1)))
        self.set(image,"m_PixelsPerUnitMultiplier",10)
        self.buttons.append(image)
        label,_ = self.label(text+" Label",tr,(12,0,bounds[2]-24,bounds[3]),text,center=True)
        return label

    def save(self):
        roots = self.records.pop(9223372036854775807)
        entries = list(self.records.items()) + [(9223372036854775807,roots)]
        self.path.write_text(self.header + "".join(f"--- !u!{self.types[id_]} &{id_}{' stripped' if self.stripped.get(id_) else ''}\n{body}" for id_,body in entries), encoding="utf-8")


def array(name, values):
    return f"  {name}:" + ("\n"+"".join(f"  - {ref(v)}\n" for v in values) if values else " []\n")


RULES = """MỤC TIÊU
Dân làng thắng khi tiêu diệt toàn bộ Cur. Cur thắng khi số Cur còn sống bằng hoặc nhiều hơn số bài Dân làng còn sống. Cur trung lập vẫn được tính.

1. HÀNH ĐỘNG
Hai phe luân phiên, mỗi lượt có 5 tim. Đặt bài mình: 2 tim; bài đối phương hoặc trung lập: 5 tim. Totem và các kỹ năng có chi phí riêng.

2. NGUYỀN RỦA
Khi bàn đầy hoặc Trưởng làng gọi, Cur chọn mục tiêu cùng hàng/cột. Ghế trống chặn lây nhiễm.

3. HIẾN TẾ
Mỗi lá của phe đang bỏ phiếu chọn một lá khác trên bàn. Không tự bỏ phiếu. Lá duy nhất trên bàn được miễn. Nhiều phiếu nhất bị hiến tế; hòa phiếu thì tất cả lá đồng hạng bị hiến tế. Kỹ năng có thể đổi kết quả.

THANH TRỪNG
Mỗi phe có 2 lần trong cả ván, loại bài nhiễm ở tay và bàn. Chỉ dùng trong lượt hành động.

RÚT BÀI
Rút bài mình hoặc trung lập hoàn tối đa 2 tim, không vượt chi phí đặt. Totem chỉ hoàn 1 tim.

ĐIỀU KHIỂN
Chuột trái để chọn hoặc kéo bài. Chuột phải để xem mô tả. Các kỹ năng đặc biệt và chi phí nằm trong mô tả từng lá. ESC đóng bảng luật và tiếp tục trận."""


def write_theme():
    THEME.write_text(f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['GameUITheme']}, type: 3}}
  m_Name: ThereasmTheme
  m_EditorClassIdentifier: 
  menuBackground: {sprite('village-night')}
  boardBackground: {sprite('ritual-table')}
  panelArtwork: {sprite('painted-panel')}
  buttonArtwork: {sprite('painted-panel')}
  villagerPortrait: {sprite('portrait-villager')}
  curPortrait: {sprite('portrait-cur')}
  localPlayPortrait: {sprite('portrait-pvp')}
  bodyFont: {font('Assets/TextMesh Pro/Fonts/LiberationSans.ttf')}
  titleFont: {font('Assets/Almendra-Regular.ttf')}
  panelPixelsPerUnit: 8
  buttonPixelsPerUnit: 10
  backgroundTint: {{r: 1, g: 1, b: 1, a: 1}}
  textColor: {color(PAPER)}
  accentColor: {color(GOLD)}
""",encoding="utf-8")


def author(path, is_menu):
    s = Scene(path)
    old_canvas = 408593582 if is_menu else 858028550
    s.set(old_canvas,"m_Name",scalar("Legacy UI — rendering disabled"))
    s.set(s.component(old_canvas,type_=223),"m_Enabled",0)
    s.set(s.component(old_canvas,script=SCRIPTS["GraphicRaycaster"]),"m_Enabled",0)
    go, canvas = s.node("Thereasm UI")
    s.canvas(go)
    s.mono(go,"CanvasScaler","""  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {x: 1600, y: 900}
  m_ScreenMatchMode: 1
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0""")
    s.mono(go,"GraphicRaycaster","  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295")
    controller = s.alloc()
    bg_go,bg_tr=s.node("Background — replace Source Image here",canvas)
    bg=s.image(bg_go,sprite("village-night" if is_menu else "ritual-table"))
    s.stretch(bg_tr)
    s.mono(bg_go,"AspectRatioFitter","  m_AspectMode: 4\n  m_AspectRatio: 1.7777778")
    _, frame=s.node("Safe Frame — 1600 x 900",canvas)
    s.position(frame,(0,0,1600,900),center=True)
    fields = {"menuController":1900930338 if is_menu else 0,"turns":0 if is_menu else 2042111136,
              "deck":0 if is_menu else 2042111134,"canvasRoot":canvas,"safeFrame":frame,"background":bg}
    levels=[]; draft_buttons=[]

    if is_menu:
        s.label("Eyebrow",frame,(80,36,1000,30),"CHIẾN THUẬT  /  SUY LUẬN  /  LỜI NGUYỀN",17,GOLD)
        s.label("Game Title",frame,(74,80,1120,108),"THEREASM",84,PAPER,title=True)
        s.label("Introduction",frame,(82,197,960,68),"Một ngôi làng. Hai phía của lời nguyền.\nChọn phe của bạn để bắt đầu.",23)
        s.button("Rules Button",frame,(1210,38,148,48),"Luật chơi",controller,"ShowRules")
        s.button("Quit Button",frame,(1374,38,146,48),"Thoát",1900930338,"QuitGame","MainMenu")
        modes=[(80,"NÊN THỬ ĐẦU TIÊN","Phe Dân làng","portrait-villager","Đấu với bot Cur. Quan sát nghi thức,\nsuy luận và dập tắt mọi nguồn lây.","Chơi Dân làng","PlayWithCurBot"),
               (572,"THỬ TÀI ĐÁNH LẠC HƯỚNG","Phe Cur","portrait-cur","Đấu với bot Dân làng. Chọn Cur bí mật,\nlây nhiễm và bảo vệ lời nguyền.","Chơi Cur","PlayWithVillBot"),
               (1064,"CÙNG MỘT MÀN HÌNH","Hai người","portrait-pvp","Mỗi người điều khiển một phe.\nLuân phiên chuẩn bị và hành động.","Chơi hai người","PlaySolo")]
        for x,eyebrow,title,portrait,desc,action,method in modes:
            _,tile=s.panel("Mode — "+title,frame,(x,302,456,300))
            s.label("Mode Tagline",tile,(28,22,400,27),eyebrow,15,GOLD)
            art_go,_=s.node("Portrait — replace Source Image here",tile,(28,65,76,116))
            meta=(ART/(portrait+".png.meta")).read_text()
            internal=re.search(r"internalID: (-?\d+)",meta)[1]
            art=s.image(art_go,f"{{fileID: {internal}, guid: {guid('Assets/Resources/ThereasmUI/'+portrait+'.png')}, type: 3}}")
            s.set(art,"m_PreserveAspect",1)
            s.label("Mode Title",tile,(126,72,302,48),title,30)
            s.label("Mode Description",tile,(126,127,302,88),desc,18,MUTED)
            s.button("Play Button",tile,(28,234,400,44),action,1900930338,method,"MainMenu")
        _,difficulty=s.panel("Difficulty",frame,(80,642,1440,132))
        s.label("Difficulty Title",difficulty,(30,28,280,30),"ĐỘ KHÓ KHI ĐẤU BOT",18,GOLD)
        s.label("Difficulty Hint",difficulty,(30,66,290,34),"Cấp 1 phù hợp để làm quen",16,MUTED)
        for i in range(5): levels.append(s.button(f"Level {i+1}",difficulty,(340+i*72,36,60,58),str(i+1),controller,"SetLevel",argument=i+1)[2])
        fields["levelLabel"]=s.label("Difficulty Description",difficulty,(760,30,640,74),"Cấp 1 · 1 Cur trong ván\nHai người chơi luôn có 2 Cur, không phụ thuộc độ khó.",20)[0]
        s.label("Controls Hint",frame,(80,824,1440,34),"CHUỘT TRÁI  Chọn / kéo bài     ·     CHUỘT PHẢI  Xem mô tả     ·     ESC  Luật chơi",18)
    else:
        tm=2042111136; dm=2042111134
        _,header=s.panel("Header — status and instructions",frame,(24,20,1552,92))
        fields["phaseLabel"]=s.label("Phase Label",header,(28,14,1496,30),"CHUẨN BỊ VÁN CHƠI",23,GOLD)[0]
        fields["instruction"]=s.label("Instruction Label",header,(28,49,1496,28),"Chọn bài trung lập, sau đó chọn Cur bí mật trước khi bắt đầu.",18)[0]
        _,side=s.panel("Controls — buttons and setup",frame,(24,132,284,744))
        s.label("Controls Title",side,(22,22,240,28),"ĐIỀU KHIỂN",20,GOLD)
        fields["endLabel"]=s.move_control(800393272,side,(22,70,240,56),"Kết thúc lượt")
        s.set(800393272,"m_IsActive",0)
        s.move_control(867535428,side,(22,70,240,56),"Bắt đầu ván")
        fields["checkLabel"]=s.move_control(1889529060,side,(22,140,240,50),"Thanh trừng (0/2)")
        hold=s.find_script(SCRIPTS["HoldToReveal"])
        if hold:
            hold_go=s.game_object(hold)
            fields["revealControl"]=hold_go
            fields["revealLabel"]=s.move_control(hold_go,side,(22,204,240,46),"Giữ để xem vai trò")
        s.button("Notes Button",side,(22,264,240,46),"Sổ tay",tm,"ToggleNote","TurnManager")
        s.button("Cemetery Button",side,(22,324,240,46),"Nghĩa trang",tm,"ToggleCemeteryView","TurnManager")
        fields["setupPanel"],setup=s.node("Setup Checklist",side,(22,392,240,176))
        s.label("Checklist Title",setup,(0,0,240,26),"VAI TRÒ BÍ MẬT",18,GOLD)
        fields["setupProgress"]=s.label("Checklist Counts",setup,(0,40,240,92),"Phe Cur: 0/1\nDân làng: 0/1\nTrung lập: 0/0",18)[0]
        s.label("Checklist Hint",setup,(0,136,240,40),"Chọn lá ở tay bài bên phải.\nThợ săn số 9 không thể là Cur.",15,MUTED)
        fields["hint"]=s.label("Context Hint",side,(22,580,240,72),"Chuột phải: xem bài\nESC: mở / đóng luật chơi",16)[0]
        fields["helpButton"]=s.button("Rules Button",frame,(46,796,116,48),"Luật chơi",controller,"ShowRules")[0]
        s.button("Menu Button",frame,(174,796,112,48),"Menu",controller,"ReturnToMenu")
        board_go,board_area=s.node("Board Area — move or resize here",frame,(332,132,1244,430))
        fields["boardArea"]=board_area
        s.parent(9897400,board_area)
        # Bounds of the authored seven seats and cemetery; later extra seats fit at runtime.
        scale=min((1244-48)/1412,(430-48)/232,1)
        s.position(9897400,(-80*scale,-175*scale,1608.672,420.784),center=True,scale=scale)
        fields["boardLayout"]=s.mono(board_go,"GameBoardLayout",f"  turns: {ref(tm)}\n  board: {ref(9897400)}\n  viewport: {ref(board_area)}\n  fitOnStart: 1\n  fitWhenSeatCountChanges: 1\n  padding: 48")
        _,dock=s.panel("Hand Dock — tabs and cards",frame,(332,584,1244,292))
        for x,name,text,method in [(22,"Cur Hand Tab","Bài phe Cur","ShowCurHand"),(210,"Village Hand Tab","Dân làng","ShowVillageHand"),(398,"Neutral Hand Tab","Trung lập","ShowNeutralHand"),(976,"Toggle Hand Button","Ẩn / mở tay bài","ToggleHand")]:
            s.button(name,dock,(x,16,246 if x==976 else 174,42),text,controller,method)
        _,cards=s.node("Hand Cards — editable origin",dock,(22,64,1200,214))
        for hand in (782377560,256364957,1750278507): s.move(hand,cards,(0,-8,0,0),center=True,scale=.72)
        s.set(dm,"angleSpread",18)
        s.set(dm,"useDockNavigation",1)
        s.set(dm,"uiTheme",f"{{fileID: 11400000, guid: {guid('Assets/UI/Themes/ThereasmTheme.asset')}, type: 2}}")
        # Save existing interactive overlays instead of recreating them in Start.
        for panel,bounds in [(746563297,(332,132,1244,744)),(1730792964,(332,132,1244,450)),(684432614,(332,132,1244,744))]:
            s.move(panel,frame,bounds)
            s.set(panel,"m_IsActive",0)
            image=s.component(panel,script=SCRIPTS['Image'])
            s.set(image,"m_Sprite",sprite("painted-panel")); s.set(image,"m_Type",1); s.set(image,"m_Color",color((1,1,1,1))); s.set(image,"m_PixelsPerUnitMultiplier",8)
            s.panels.append(image)
        for slot,x,y in [(1868587133,.25,195),(1154941076,.75,195),(513601359,.25,220),(1021825899,.5,220),(439567711,.75,220)]:
            for name in ('m_AnchorMin','m_AnchorMax'): s.set(slot,name,vector((x,1)))
            s.set(slot,'m_Pivot',vector((.5,.5))); s.set(slot,'m_AnchoredPosition',vector((0,-y))); s.set(slot,'m_LocalScale',vector((1,1,1)))
        for left,x in ((True,60),(False,682)):
            draft_buttons.append(s.button("Choose Left" if left else "Choose Right",s.component(746563297,type_=224),(x,650,500,50),"Chọn lá bên trái" if left else "Chọn lá bên phải",controller,"PickDraftLeft" if left else "PickDraftRight")[2])
        s.parent(s.component(1927024418,type_=224),frame)
        s.parent(s.component(1053673839,type_=224),frame); s.stretch(s.component(1053673839,type_=224))
        fields["inputShield"],_=s.node("Bot Input Shield",frame,(0,0,1600,900),active=False)
        s.image(fields["inputShield"],tint=(0,0,0,0),raycast=True)
        fields["resultPanel"],result=s.panel("Result Panel — preview by enabling",frame,(360,250,880,370),active=False)
        fields["resultLabel"]=s.label("Result Label",result,(40,40,800,160),"VÁN CHƠI KẾT THÚC",28,GOLD,center=True)[0]
        s.button("Next Match Button",result,(126,250,300,54),"Chơi tiếp",controller,"NextMatch")
        s.button("Result Menu Button",result,(454,250,300,54),"Về menu",controller,"ReturnToMenu")
        s.set(tm,'winPanel',ref(fields['resultPanel']))
        # The previous winText remains the gameplay message source, hidden by Legacy Canvas.
        s.set(1471685473,'m_IsActive',0)
        # Keep the original tab/note managers alive: only the old Canvas renderer is disabled.

    # A fully authored modal, including its scroll viewport and close button.
    fields['rulesPanel'],rules=s.node("Rules Panel — preview by enabling",frame,(0,0,1600,900),active=False)
    s.image(fields['rulesPanel'],tint=(0,0,0,.9),raycast=True)
    s.canvas(fields['rulesPanel'],3000)
    s.mono(fields['rulesPanel'],"GraphicRaycaster","  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295")
    _,sheet=s.panel("Rulebook",rules,(170,70,1260,760))
    s.label("Rulebook Title",sheet,(44,32,1172,64),"LUẬT CHƠI NHANH",36,GOLD)
    s.label("Rulebook Hint",sheet,(46,105,1168,32),"Cuộn để xem đầy đủ · Trận đấu đang tạm dừng",18,MUTED)
    view_go,view=s.node("Scroll Viewport",sheet,(46,156,1168,490))
    s.image(view_go,tint=(0,0,0,0),raycast=True)
    s.mono(view_go,"RectMask2D","  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}")
    _,content=s.label("Rules Text — edit wording here",view,(0,0,1140,850),RULES,22)
    s.mono(s.game_object(content),"ContentSizeFitter","  m_HorizontalFit: 0\n  m_VerticalFit: 2")
    s.mono(view_go,"ScrollRect",f"  m_Content: {ref(content)}\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 32\n  m_Viewport: {ref(view)}\n  m_HorizontalScrollbar: {{fileID: 0}}\n  m_VerticalScrollbar: {{fileID: 0}}\n  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 0\n  m_HorizontalScrollbarSpacing: -3\n  m_VerticalScrollbarSpacing: -3\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []")
    s.button("Close Rules Button",sheet,(450,676,360,52),"Đã hiểu · Tiếp tục",controller,"CloseRules")

    # The cursor uses a saved parent Canvas; do not add a Canvas to the Image.
    cursor=s.find_script(SCRIPTS['CustomCusor'])
    if cursor:
        cursor_go,cursor_canvas=s.node("Cursor Canvas",canvas)
        s.stretch(cursor_canvas); s.canvas(cursor_go,5000)
        s.parent(s.component(s.game_object(cursor),type_=224),cursor_canvas)
    target=s.find_script(SCRIPTS['TargetingArrow'])
    if target: s.parent(s.component(s.game_object(target),type_=224),canvas)

    data=f"  theme: {{fileID: 11400000, guid: {guid('Assets/UI/Themes/ThereasmTheme.asset')}, type: 2}}\n"
    data+=''.join(f"  {key}: {ref(value)}\n" for key,value in fields.items())
    data+=array('levelButtons',levels)+array('draftButtons',draft_buttons)+array('panelImages',s.panels)+array('buttonImages',s.buttons)
    data+='  adaptToSafeArea: 1\n'
    s.mono(go,"GameStartUI",data,controller)
    s.save()
    return dict(scene=path,controller=controller,canvas=canvas,frame=frame,objects=s.manifest)


if __name__ == '__main__':
    # Preflight before writing the theme as well as before touching either scene.
    for scene_name in ('MenuScenes', 'MainBoard'):
        source = (ROOT / f'Assets/Scenes/{scene_name}.unity').read_text(encoding='utf-8-sig')
        if 'm_Name: "Thereasm UI"' in source:
            raise SystemExit('UI already authored. Refusing to overwrite scenes or the edited theme.')
    write_theme()
    manifests=[author('Assets/Scenes/MenuScenes.unity',True),author('Assets/Scenes/MainBoard.unity',False)]
    output=ROOT/'Tools/authored_ui_manifest.json'
    output.write_text(json.dumps(manifests,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Authored and saved MenuScenes/MainBoard. UI and sprites now exist in Edit Mode.')

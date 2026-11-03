"""Configure online lobby UI components and Photon network configuration references."""
from migrate_scene_ui import *

for name in ('PhotonOnlineConfig', 'OnlineSession', 'OnlineLobbyView', 'OnlineGameBridge'):
    ensure_meta(ROOT / f'Assets/Scripts/{name}.cs')
    SCRIPTS[name] = guid(f'Assets/Scripts/{name}.cs')
SCRIPTS['InputField'] = guid(UGUI + 'InputField.cs')

def named(scene, name):
    return next(i for i, body in scene.records.items() if scene.types[i] == 1 and
                scene.value(i, 'm_Name').strip('"') == name)

def append_refs(scene, controller, field, values):
    pattern = rf'(?m)^  {field}:(?: \[\]\n|\n(?:  - .*\n)*)'
    match = re.search(pattern, scene.records[controller])
    old = list(map(int, re.findall(r'fileID: (\d+)', match[0]))) if match else []
    scene.records[controller] = re.sub(pattern, lambda _: array(field, old + values), scene.records[controller])

menu = Scene('Assets/Scenes/MenuScenes.unity', allow_migrated=True)
board = Scene('Assets/Scenes/MainBoard.unity', allow_migrated=True)
if any('m_Name: "Online Lobby"\n' in b for b in menu.records.values()):
    raise RuntimeError('Already authored. Preserve subsequent manual scene edits.')

config = ROOT / 'Assets/Networking/PhotonOnlineConfig.asset'
config.parent.mkdir(exist_ok=True)
ensure_meta(config.parent)
ensure_meta(config)
config.write_text(f'''%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['PhotonOnlineConfig']}, type: 3}}
  m_Name: PhotonOnlineConfig
  m_EditorClassIdentifier:
  appIdFusion: REMOVED
  appVersion: thereasm-online-1
  region: asia
  boardScene: MainBoard
  snapshotInterval: 0.5
''', encoding='utf-8')

session_go, _ = menu.node('Photon Session — configuration', rect=(0,0,0,0))
menu.mono(session_go, 'OnlineSession', f'  settings: {{fileID: 11400000, guid: {guid(config)}, type: 2}}')
view = menu.find_script(SCRIPTS['GameStartUI'])
frame = menu.component(named(menu, 'Safe Frame — 1600 x 900'), type_=224)
menu.button('Online Play Button', frame, (1210,197,310,58), 'Chơi online · 2 người', view, 'OpenOnline')
modal_go, modal = menu.node('Online Lobby', frame, active=False)
menu.image(modal_go, tint=(0.015,0.008,0.025,.96), raycast=True)
menu.canvas(modal_go, 2500)
menu.mono(modal_go, 'GraphicRaycaster', '  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295')
_, panel = menu.panel('Online Room Panel — edit layout here', modal, (300,130,1000,640))
lobby = menu.mono(modal_go, 'OnlineLobbyView')
menu.label('Online Title', panel, (40,24,920,54), 'CHƠI ONLINE · 2 NGƯỜI', 30, GOLD, center=True)
menu.label('Room Code Caption', panel, (40,102,920,30), 'Mã phòng · để trống khi tạo để lấy mã tự động', 20)
input_go, input_rect = menu.node('Room Code Input', panel, (40,144,920,58))
input_image = menu.image(input_go, sprite('painted-panel'), sliced=True, ppu=10, raycast=True)
text, _ = menu.label('Room Code Text', input_rect, (24,0,872,58), '', 23, center=True)
placeholder, _ = menu.label('Room Code Placeholder', input_rect, (24,0,872,58), 'VD: LANG01', 23, MUTED, center=True)
# Reuse serialized Selectable fields without a click handler.
template = next(b for i,b in menu.records.items() if f'guid: {SCRIPTS["Button"]},' in b)
selectable = template[template.index('  m_Navigation:'):template.index('  m_OnClick:')]
selectable = re.sub(r'(?m)^  m_TargetGraphic: .*', f'  m_TargetGraphic: {ref(input_image)}', selectable)
input_field = menu.mono(input_go, 'InputField', selectable + f'''  m_TextComponent: {ref(text)}
  m_Placeholder: {ref(placeholder)}
  m_ContentType: 0
  m_InputType: 0
  m_AsteriskChar: 42
  m_KeyboardType: 0
  m_LineType: 0
  m_HideMobileInput: 0
  m_HideSoftKeyboard: 0
  m_CharacterValidation: 0
  m_CharacterLimit: 20
  m_OnEndEdit:
    m_PersistentCalls:
      m_Calls: []
  m_OnSubmit:
    m_PersistentCalls:
      m_Calls: []
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []
  m_CaretColor: {{r: 1, g: 0.85, b: 0.6, a: 1}}
  m_CustomCaretColor: 1
  m_SelectionColor: {{r: 0.5, g: 0.3, b: 0.15, a: 0.75}}
  m_Text: ""
  m_CaretBlinkRate: 0.85
  m_CaretWidth: 2
  m_ReadOnly: 0
  m_ShouldActivateOnSelect: 1
''')
role, _ = menu.label('Online Role Status', panel, (40,226,920,56), 'Phe của bạn: CUR\nChọn phe đang có người giữ sẽ đổi vai của cả hai.', 21, GOLD, center=True)
for name, bounds, label, method in (
    ('Choose Cur', (40,298,440,46), 'Chọn phe Cur', 'ChooseCur'),
    ('Choose Village', (520,298,440,46), 'Chọn phe Dân làng', 'ChooseVillage')):
    menu.button(name, panel, bounds, label, lobby, method, 'OnlineLobbyView')
buttons = {}
for name, bounds, label, method in (
    ('Create Room', (40,366,284,52), 'Tạo phòng', 'CreateRoom'),
    ('Join Room', (354,366,284,52), 'Vào phòng', 'JoinRoom'),
    ('Start Online Match', (668,366,292,52), 'Bắt đầu ván', 'StartMatch'),
    ('Leave Online Room', (40,556,440,48), 'Rời phòng', 'LeaveRoom'),
    ('Close Online Lobby', (520,556,440,48), 'Đóng', 'Close')):
    buttons[method] = menu.button(name, panel, bounds, label, lobby, method, 'OnlineLobbyView')[2]
status, _ = menu.label('Online Connection Status', panel, (40,436,920,100), 'Chọn phe, tạo phòng hoặc nhập mã phòng của bạn.', 20, center=True)
for field, value in dict(panel=modal_go, roomCode=input_field, status=status, role=role,
    createButton=buttons['CreateRoom'], joinButton=buttons['JoinRoom'],
    startButton=buttons['StartMatch'], leaveButton=buttons['LeaveRoom']).items(): menu.set(lobby, field, ref(value))
menu.set(view, 'onlineLobby', ref(lobby))
menu.set(view, 'rulesHint', ref(menu.component(named(menu, 'Rulebook Hint'), script=SCRIPTS['Text'])))
append_refs(menu, view, 'panelImages', menu.panels)
append_refs(menu, view, 'buttonImages', menu.buttons + [input_image])

view = board.find_script(SCRIPTS['GameStartUI'])
result = board.component(named(board, 'Result Panel — preview by enabling'), type_=224)
swap, _, _, _ = board.button('Swap Roles For Next Match', result, (280,318,320,36), 'Đổi phe ván sau', view, 'SwapOnlineRoles')
board.set(swap, 'm_IsActive', 0)
board.set(view, 'swapRoleButton', ref(swap))
board.set(view, 'menuButton', ref(named(board, 'Menu Button')))
board.set(view, 'handDock', ref(board.component(named(board, 'Hand Dock — tabs and cards'), type_=224)))
board.set(view, 'rulesHint', ref(board.component(named(board, 'Rulebook Hint'), script=SCRIPTS['Text'])))
board.mono(2042111133, 'OnlineGameBridge', '  turns: {fileID: 2042111136}\n  deck: {fileID: 2042111134}')
append_refs(board, view, 'buttonImages', board.buttons)
menu.save()
board.save()
print('Authored online room UI, scene bridge, role swap button and Photon config.')

"""Initialize TutorialBoard scene layout and default lesson setup."""
from migrate_scene_ui import *
import shutil

target = ROOT / 'Assets/Scenes/TutorialBoard.unity'
if target.exists(): raise RuntimeError('TutorialBoard already exists. Edit the saved scene/lesson in Unity instead.')
for name in ('TutorialController', 'TutorialLesson', 'TutorialRules'):
    ensure_meta(ROOT / f'Assets/Scripts/{name}.cs')
    SCRIPTS[name] = guid(f'Assets/Scripts/{name}.cs')

def named(s, name):
    return next(i for i,b in s.records.items() if s.types[i] == 1 and s.value(i,'m_Name').strip('"') == name)
def rect_of(s, name): return s.component(named(s,name),type_=224)
def text_of(s, name): return s.component(named(s,name),script=SCRIPTS['Text'])
def raycaster(s, go):
    s.mono(go,'GraphicRaycaster','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295')

steps = [
('Làm quen bàn chơi', 'Bấm Bắt đầu học. Bạn sẽ tự thao tác cả hai phe trên một bàn bài mẫu.', 'Viền vàng: điểm bắt đầu. Viền xanh: nơi thả. Bài học dùng 7 lá cố định, bỏ qua chọn bài trung lập.', 'Bắt đầu từ việc chọn vai trò bí mật.'),
('Chọn Cur thứ nhất', 'Bấm CHUỘT TRÁI vào lá SỐ 1 có viền vàng trong khay bài.', 'Cur là vai trò bí mật của một lá bài. Trong ván hai người, cần chọn một Cur ở mỗi nhóm bài gốc.', 'Lá số 1 đã trở thành Cur bí mật.'),
('Mở nhóm Dân làng', 'Bấm tab DÂN LÀNG ở đầu khay bài bên dưới.', 'Tab đổi nhóm bài đang xem. Bạn có thể đánh cả bài nhóm khác, nhưng sẽ tốn nhiều tim hơn.', 'Bạn đang xem nhóm bài Dân làng.'),
('Một Cur ngụy trang', 'Bấm CHUỘT TRÁI vào lá SỐ 11 có viền vàng.', 'Lá thuộc nhóm Dân làng vẫn có thể mang vai trò Cur. Thợ săn số 9 không được chọn làm Cur.', 'Lá số 11 là Cur ngụy trang trong nhóm Dân làng.'),
('Bắt đầu ván', 'Đã có đủ 2 Cur. Bấm BẮT ĐẦU VÁN ở nút đang sáng.', 'Trong bài học này Dân làng đi trước. Mỗi lượt hành động có 5 tim để đặt bài hoặc dùng kỹ năng.', 'Lượt Dân làng bắt đầu với 5 tim.'),
('Xem một lá bài', 'Bấm CHUỘT PHẢI vào lá SỐ 10 trong khay bài.', 'Chuột phải phóng to lá bài và mở mô tả. Xem bài không tốn tim, không phải là dùng kỹ năng.', 'Mặt bài và mô tả đã được phóng to.'),
('Đóng phần xem bài', 'Bấm CHUỘT PHẢI lần nữa vào lá đang được phóng to.', 'Đóng phần xem trước khi kéo bài. Lá sẽ trở lại đúng khay và kích thước ban đầu.', 'Bạn đã biết cách xem và đóng mô tả.'),
('Đặt lá đầu tiên', 'GIỮ CHUỘT TRÁI ở lá SỐ 10, kéo đến GHẾ VIỀN XANH rồi thả.', 'Đặt bài thuộc phe đang chơi tốn 2 tim. Ghế là vị trí chứa một lá trên bàn, không phải khay bài.', 'Đặt thành công: 5 tim → 3 tim.'),
('Đưa Thợ săn lên bàn', 'Kéo CHUỘT TRÁI lá SỐ 9 đến ghế có viền xanh rồi thả.', 'Thợ săn chỉ dùng kỹ năng khi đã ở trên bàn. Đặt lá thứ hai tốn thêm 2 tim; bạn sẽ còn 1 tim.', 'Thợ săn đã lên bàn. Dân làng còn 1/5 tim.'),
('Chuyển lượt', 'Bấm KẾT THÚC LƯỢT ở nút đang sáng.', 'Bạn không bắt buộc tiêu hết tim. Hết lượt, đối phương được hành động với 5 tim.', 'Đến lượt phe Cur; khay bài đã đổi theo lượt.'),
('Nhìn vai trò bí mật', 'GIỮ CHUỘT TRÁI nút XEM VAI TRÒ ít nhất một giây, rồi THẢ RA.', 'Phe Cur dùng nút này để xem Cur và nhiễm bệnh. Thả nút sẽ che lại vai trò. Online chỉ phe Cur được xem.', 'Giữ để xem, thả để giấu: vai trò lại được che kín.'),
('Đặt Cur lên bàn', 'Kéo CHUỘT TRÁI lá SỐ 1 vào ghế có viền xanh.', 'Cur vẫn ngụy trang bằng mặt bài thường. Đặt lá của phe Cur trong lượt Cur cũng tốn 2 tim.', 'Lá số 1 đã ngồi trên bàn dưới dạng ngụy trang.'),
('Trở lại Dân làng', 'Bấm KẾT THÚC LƯỢT để chuyển sang Dân làng.', 'Ở lượt mới, Dân làng có lại 5 tim. Số tim này đủ để thử kỹ năng Thợ săn.', 'Dân làng có 5 tim để sử dụng kỹ năng.'),
('Dùng kỹ năng Thợ săn', 'GIỮ CHUỘT PHẢI ở lá SỐ 9, kéo mũi ngắm đến lá SỐ 1 rồi thả.', 'Chuột phải KÉO là nhắm kỹ năng; chuột phải BẤM là xem bài. Phát bắn tốn 5 tim và đưa mục tiêu vào nghĩa trang.', 'Đã bắn đúng mục tiêu và dùng hết 5 tim.'),
('Mở nghĩa trang', 'Bấm nút NGHĨA TRANG đang sáng bên trái.', 'Các lá bị tiêu diệt nằm ở đây. Việc xem nghĩa trang không tốn tim. Một số kỹ năng có thể tương tác với bài chết.', 'Lá số 1 đã nằm trong nghĩa trang.'),
('Đóng nghĩa trang', 'Bấm lại nút NGHĨA TRANG để trở về bàn chơi.', 'Bạn có thể mở lại khi cần kiểm tra lá đã bị loại. Bài chết không được tiếp tục đặt hoặc bỏ phiếu.', 'Bạn đã trở về bàn chơi.'),
('Thử nghi thức', 'Bấm THỬ NGHI THỨC. Bài học sẽ đặt sẵn lá số 11 và số 2 để luyện tập.', 'Trong ván thật, nghi thức đến khi bàn đầy hoặc Trưởng làng gọi. Đây là tình huống mẫu, không trừ tim của bạn.', 'Tình huống mẫu đã sẵn sàng: đang tập phe Cur.'),
('Nguyền rủa mục tiêu', 'GIỮ CHUỘT TRÁI ở lá SỐ 11, kéo sang lá SỐ 9 viền xanh rồi thả.', 'Lá 11 là Cur đã chọn. Cur lây cho mục tiêu cùng hàng/cột; ghế trống chặn đường lây. Nhiễm bệnh khác với vai trò Cur.', 'Thợ săn số 9 đã bị nhiễm; lá số 11 vẫn là Cur.'),
('Sang hiến tế', 'Bấm SANG HIẾN TẾ ở nút đang sáng.', 'Hai phe lần lượt bỏ phiếu. Mỗi lá đang ở trên bàn của nhóm đến lượt phải chọn một lá khác.', 'Đến lượt bỏ phiếu của nhóm Dân làng.'),
('Phiếu của lá số 10', 'Kéo CHUỘT TRÁI từ lá SỐ 10 đến lá SỐ 2 viền xanh rồi thả.', 'Mũi tên lúc này là phiếu bầu, không phải di chuyển bài. Không được bỏ phiếu cho chính mình.', 'Lá số 10 đã bỏ phiếu vào lá số 2.'),
('Phiếu của Thợ săn', 'Kéo CHUỘT TRÁI từ lá SỐ 9 đến lá SỐ 2.', 'Mỗi lá bỏ một phiếu riêng. Đã dùng kỹ năng hoặc đang nhiễm bệnh vẫn phải bỏ phiếu nếu lá còn sống trên bàn.', 'Lá số 9 đã bỏ phiếu vào lá số 2.'),
('Phiếu cuối của nhóm', 'Kéo CHUỘT TRÁI từ lá SỐ 11 đến lá SỐ 2.', 'Lá 11 mang vai trò Cur nhưng thuộc nhóm Dân làng, nên vẫn bỏ phiếu ở lượt của nhóm này.', 'Cả 3 lá của nhóm Dân làng đã bỏ phiếu.'),
('Chốt phiếu', 'Bấm CHỐT PHIẾU để chuyển cho phe còn lại.', 'Nút chỉ chốt được khi đủ phiếu. Khi chơi thật, vị trí phiếu của đối phương được giữ kín trước tổng kết.', 'Phiếu Dân làng đã chốt; đến lượt nhóm phe Cur.'),
('Lượt của đối phương', 'Bấm XEM LƯỢT ĐỐI PHƯƠNG. Bài học sẽ bỏ một phiếu mẫu từ lá số 2.', 'Lá số 2 bầu cho lá số 10. Sau khi cả hai phe chốt, game tự đếm phiếu và áp dụng kỹ năng ảnh hưởng kết quả.', 'Hai phe đã chốt. Hãy quan sát kết quả tổng kết.'),
('Quan sát tổng kết', 'Đợi game đếm phiếu, hiến tế lá số 2 rồi thu các lá sống về tay.', 'Lá nhiều phiếu nhất bị hiến tế. Nếu hòa ở số phiếu cao nhất, các lá đồng hạng đều bị hiến tế.', 'Lá số 2 nhận 3 phiếu và bị hiến tế; các lá sống đã về tay.'),
('Thử thanh trừng', 'Bài học chuyển sang Dân làng. Bấm THANH TRỪNG ở nút đang sáng.', 'Thanh trừng loại mọi lá đang nhiễm bệnh, kể cả trong khay ẩn. Mỗi phe chỉ có 2 lần mỗi ván; nó không tự tìm Cur.', 'Lá số 9 bị loại vì nhiễm bệnh. Dân làng còn 1/2 lần thanh trừng.'),
('Bạn đã biết cách chơi', 'Bạn đã tự chọn vai trò, đặt bài, dùng kỹ năng, nguyền rủa và bỏ phiếu. Hãy thử một ván với bot!', 'Dân làng thắng khi hết Cur. Cur thắng khi số Cur sống ≥ số Dân làng sống không phải Cur. Bài học đã kết thúc; chưa xử thắng thua.', 'Hoàn thành hướng dẫn.')
]
assert len(steps) == 27
lesson_path = ROOT / 'Assets/UI/Tutorial/FirstMatchLesson.asset'
lesson_path.parent.mkdir(parents=True,exist_ok=True)
ensure_meta(lesson_path.parent); ensure_meta(lesson_path)
asset = f'''%YAML 1.1
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
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['TutorialLesson']}, type: 3}}
  m_Name: FirstMatchLesson
  m_EditorClassIdentifier:
  steps:
'''
for index,(title,instruction,purpose,success) in enumerate(steps):
    asset += f'  - stage: {index}\n    title: {scalar(title)}\n    instruction: {scalar(instruction)}\n    purpose: {scalar(purpose)}\n    success: {scalar(success)}\n'
asset += '  cards:\n' + ''.join(f'  - {{fileID: 11400000, guid: {guid(f"Assets/ScriptableObjects/Villager{number}.asset")}, type: 2}}\n' for number in (1,2,3,9,10,11,12))
asset += '  practiceScene: MainBoard\n  menuScene: MenuScenes\n'
lesson_path.write_text(asset,encoding='utf-8')

shutil.copyfile(ROOT/'Assets/Scenes/MainBoard.unity',target)
ensure_meta(target)
s = Scene('Assets/Scenes/TutorialBoard.unity',allow_migrated=True)
view = s.find_script(SCRIPTS['GameStartUI'])
frame = rect_of(s,'Safe Frame — 1600 x 900')
side = rect_of(s,'Controls — buttons and setup')
controlled = [i for i,b in s.records.items() if s.types[i]==114 and f'guid: {SCRIPTS["Button"]},' in b]
# Preserve the real gameplay buttons and their persistent events, move them into a shared action slot.
tm,dm=2042111136,2042111134
s.set(dm,'separateHandCards',1)
s.set(dm,'separatedCardGap',24)
for modal in (684432614,1730792964,746563297,1927024418): s.set(modal,'m_IsActive',0)
controls = {
    'startControl':867535428, 'endControl':800393272, 'checkControl':1889529060,
    'revealControl':int(re.search(r'fileID: (\d+)',s.value(view,'revealControl'))[1]),
    'cemeteryControl':named(s,'Cemetery Button')
}
for child in s.children(side): s.set(s.game_object(child),'m_IsActive',0)
for name in ('Menu Button','Rules Button','Notes Button','Toggle Hand Button'):
    s.set(named(s,name),'m_IsActive',0)
# TabManager's global mouse handling would close the inspected card before PointerClick.
tab_guid=guid('Assets/Scripts/TabManager.cs')
tab=s.find_script(tab_guid)
if tab: s.set(tab,'m_Enabled',0)
for key,go in controls.items():
    s.move(go,side,(20,584,244,48))
    s.set(go,'m_IsActive',0)
# UI controller must not raise the old hidden menu/help buttons over tutorial panels.
s.set(view,'menuButton',ref(0)); s.set(view,'helpButton',ref(0))
s.set(s.component(s.game_object(side),type_=223),'m_Enabled',1) if s.component(s.game_object(side),type_=223) else s.canvas(s.game_object(side),2200)
raycaster(s,s.game_object(side))

guide_go, guide_rect = s.node('Tutorial Guide — editable instructions',side,rect=(0,0,284,744))
controller = s.mono(guide_go,'TutorialController')
progress,_=s.label('Tutorial Progress',guide_rect,(20,20,244,28),'BƯỚC 01 / 27',17,GOLD)
_,track_rect=s.node('Tutorial Progress Track',guide_rect,(20,54,244,5))
s.image(s.game_object(track_rect),tint=(.18,.11,.13,1))
fill_go,fill_rect=s.node('Tutorial Progress Fill',track_rect,(0,0,244,5))
fill=s.image(fill_go,tint=GOLD)
s.set(fill_rect,'m_AnchorMin',vector((0,0)));s.set(fill_rect,'m_AnchorMax',vector((1/27,1)))
s.set(fill_rect,'m_SizeDelta',vector((0,0)));s.set(fill_rect,'m_AnchoredPosition',vector((0,0)))
title,_=s.label('Tutorial Step Title',guide_rect,(20,76,244,82),steps[0][0],24,GOLD)
instruction,_=s.label('Tutorial Action Instruction',guide_rect,(20,172,244,176),steps[0][1],20)
purpose,_=s.label('Tutorial Why This Matters',guide_rect,(20,366,244,145),steps[0][2],17,MUTED)
feedback,_=s.label('Tutorial Success Feedback',guide_rect,(20,522,244,54),'Mỗi lần chỉ thực hiện thao tác được chỉ sáng.',16,(.62,.95,.72,1))
continue_go,_,continue_button,continue_label=s.button('Tutorial Continue',guide_rect,(20,584,244,48),'Bắt đầu học',controller,'Continue','TutorialController')
practice_go,_,practice_button,_=s.button('Tutorial Practice With Bot',guide_rect,(20,584,244,48),'Chơi thử với bot',controller,'Practice','TutorialController')
s.set(practice_go,'m_IsActive',0)
s.button('Tutorial Restart',guide_rect,(20,644,116,40),'Học lại',controller,'Restart','TutorialController')
s.button('Tutorial Exit',guide_rect,(148,644,116,40),'Về menu',controller,'Exit','TutorialController')
s.button('Tutorial Rulebook',guide_rect,(20,694,244,30),'Xem luật đầy đủ',view,'ShowRules')
# Guide text has no blocking backdrop: actual action controls remain sibling objects beneath it.
overlay_go,overlay=s.node('Tutorial Focus — follows real targets',frame)
s.stretch(overlay);s.canvas(overlay_go,2300)
focus=[]
for name,tint,caption in [('Source',GOLD,'BẮT ĐẦU Ở ĐÂY'),('Target',(.52,1,.72,1),'THẢ VÀO ĐÂY')]:
    go,tr=s.node(f'Tutorial {name} Frame',overlay)
    s.position(tr,(0,0,100,100),center=True)
    img=s.image(go,sprite('painted-panel'),tint=tint,sliced=True,ppu=12)
    s.set(img,'m_FillCenter',0)
    label,labtr=s.label(f'Tutorial {name} Caption',tr,(-20,-30,280,26),caption,15,tint,center=True)
    s.set(labtr,'m_AnchorMin',vector((.5,1)));s.set(labtr,'m_AnchorMax',vector((.5,1)));s.set(labtr,'m_Pivot',vector((.5,0)))
    s.set(labtr,'m_AnchoredPosition',vector((0,4)))
    focus.append((tr,img))
arrow,arrow_rect=s.label('Tutorial Direction Arrow',overlay,(0,0,80,64),'→',56,GOLD,center=True)
s.position(arrow_rect,(0,0,80,64),center=True)
fields=dict(lesson=f'{{fileID: 11400000, guid: {guid(lesson_path)}, type: 2}}',turns=ref(tm),deck=ref(dm),overlay=ref(overlay),
    sourceFrame=ref(focus[0][0]),targetFrame=ref(focus[1][0]),directionArrow=ref(arrow_rect),sourceOutline=ref(focus[0][1]),
    targetOutline=ref(focus[1][1]),progressFill=ref(fill),progress=ref(progress),title=ref(title),instruction=ref(instruction),
    purpose=ref(purpose),feedback=ref(feedback),continueLabel=ref(continue_label),header=ref(text_of(s,'Phase Label')),headerHint=ref(text_of(s,'Instruction Label')),
    continueButton=ref(continue_button),practiceButton=ref(practice_button),villageTab=ref(s.component(named(s,'Village Hand Tab'),script=SCRIPTS['Button'])),
    setupPanel=s.value(view,'setupPanel'),resultPanel=s.value(view,'resultPanel'))
fields.update({key:ref(value) for key,value in controls.items()})
for key,value in fields.items(): s.set(controller,key,value)
s.records[controller] += array('controlledButtons',controlled)
# Empty lesson panels never cover the hand or playable board. Completion uses a shorter body.
s.set(view,'hint',ref(0))
s.save()

menu=Scene('Assets/Scenes/MenuScenes.unity',allow_migrated=True)
menu_controller=menu.find_script(guid('Assets/Scripts/MainMenu.cs'))
menu.set(menu_controller,'tutorialSceneName','TutorialBoard')
menu_frame=rect_of(menu,'Safe Frame — 1600 x 900')
menu.position(rect_of(menu,'Introduction'),(82,192,1000,52))
menu.records[text_of(menu,'Introduction')]=menu.records[text_of(menu,'Introduction')].replace('m_FontSize: 23','m_FontSize: 20')
menu.button('Tutorial Play Button',menu_frame,(80,250,456,40),'Học cách chơi · hướng dẫn từng bước',menu_controller,'PlayTutorial','MainMenu')
menu.save()
build=ROOT/'ProjectSettings/EditorBuildSettings.asset'
text=build.read_text()
text=text.replace('  m_configObjects:',f'  - enabled: 1\n    path: Assets/Scenes/TutorialBoard.unity\n    guid: {guid(target)}\n  m_configObjects:')
build.write_text(text,encoding='utf-8')
print('Authored TutorialBoard, seven-card exercise, 27 editable steps, focus UI and menu entry.')

"""Generate bilingual action rejection messages and UI handlers."""
from pathlib import Path
import sys, json, re, shutil
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Tools'))
from migrate_scene_ui import Scene, guid, ensure_meta, SCRIPTS

translations = [
("{0}: this action needs {1} hearts; you have {2}. Choose a cheaper action or end your turn.", "{0}: thao tác này cần {1} tim, bạn còn {2} tim. Hãy chọn việc ít tốn tim hơn hoặc kết thúc lượt."),
("The match has ended. Choose Rematch or Menu.", "Ván chơi đã kết thúc. Chọn Chơi tiếp hoặc Menu nhé."),
("Votes are being resolved. Wait for the next turn.", "Phiếu đang được tổng kết. Chờ lượt tiếp theo nhé."),
("It is your opponent's turn. Wait for your turn before playing or moving cards.", "Đang là lượt đối phương. Chờ tới lượt mình để đặt hoặc di chuyển bài nhé."),
("Cards in the cemetery cannot be moved. Use a resurrection skill to bring them back.", "Không thể kéo bài đã chết ra khỏi nghĩa trang. Cần kỹ năng hồi sinh để đưa chúng trở lại."),
("{0} is held by Elder. Move it outside Elder's influence with a card effect, or remove Elder first.", "{0} đang bị Elder giữ. Hãy dùng hiệu ứng bài đẩy nó ra khỏi vùng của Elder, hoặc đưa Elder đi trước."),
("{0} cannot be moved by hand while its extra seats are occupied. Empty those seats first; card effects can still push it.", "Ghế phụ của {0} đang có người nên không thể kéo bằng tay. Dọn trống ghế phụ trước nhé; hiệu ứng bài vẫn có thể đẩy nó."),
("{0} is resting after being played. You can move or retrieve it on your next action turn.", "{0} vừa được đặt xuống nên chưa thể kéo đi hay thu hồi. Đến lượt hành động tiếp theo của bạn, nó sẽ sẵn sàng."),
("Choose the secret roles and start the match before moving cards.", "Hãy chọn xong vai trò bí mật và bắt đầu ván trước khi di chuyển bài."),
("Cards cannot move during the curse ritual. Drag from a seated Cursermancer to spread the curse, or continue to Sacrifice.", "Nghi thức Nguyền rủa không cho di chuyển bài. Kéo từ Cursermancer trên bàn để lây nhiễm, hoặc chuyển sang Hiến tế."),
("Cards cannot move during Sacrifice. Drag from each of your seated cards to another living card to vote.", "Không thể di chuyển bài trong Hiến tế. Kéo từ từng lá của bạn trên bàn tới một lá còn sống khác để bỏ phiếu."),
("Close the enlarged card with right-click before moving cards.", "Bấm chuột phải để đóng lá đang phóng to rồi hãy di chuyển bài nhé."),
("This card is face down. Its information and actions are unavailable.", "Lá này đang úp nên chưa thể xem thông tin hay thực hiện thao tác."),
("Play this card onto a seat before using its skill. Right-click without dragging to read it.", "Lá này cần ngồi trên ghế mới dùng được kỹ năng. Bấm chuột phải, không kéo, để đọc mô tả nhé."),
("This card has no active skill. Its passive effect works automatically when its conditions are met.", "Lá này không có kỹ năng cần bấm để dùng. Hiệu ứng bị động sẽ tự kích hoạt khi đủ điều kiện."),
("Start the curse arrow from one of your seated Cursermancers.", "Hãy bắt đầu mũi tên lây nhiễm từ một Cursermancer của bạn đang ngồi trên bàn."),
("Only your living cards on the board can cast your votes. Drag from one of them to another living card.", "Chỉ bài còn sống của bạn trên bàn mới được bỏ phiếu. Kéo từ một lá đó tới một lá còn sống khác nhé."),
("GameMaster cannot occupy an extra seat. Choose one of the seven main seats.", "GameMaster không thể ngồi ghế phụ. Hãy chọn một trong bảy ghế chính nhé."),
("{0} is resting and cannot be swapped yet. Wait until its next action turn.", "{0} đang nghỉ nên chưa thể đổi chỗ. Chờ đến lượt hành động tiếp theo của phe lá bài này nhé."),
("These two cards have already swapped this turn. Choose a different pair or wait for the next turn.", "Hai lá này đã đổi chỗ với nhau trong lượt này rồi. Chọn cặp khác hoặc chờ sang lượt tiếp theo nhé."),
("This card is no longer available. Choose another card.", "Lá này không còn dùng được ở vị trí hiện tại. Hãy chọn lá khác nhé."),
("{0}'s printed skill is blocked by Saboteur. Move it out of Saboteur's influence first.", "Kỹ năng gốc của {0} đang bị Saboteur khóa. Hãy đưa nó ra khỏi vùng ảnh hưởng của Saboteur trước."),
("Choose a living target on the board, not a card in a hand or the cemetery.", "Hãy chọn mục tiêu còn sống trên bàn. Bài trên tay và trong nghĩa trang không phải mục tiêu hợp lệ."),
("{0}: left-click the card itself to use this skill; do not drag onto another card.", "{0}: bấm chuột trái ngay trên lá này để dùng kỹ năng, không kéo sang lá khác nhé."),
("{0}: hold the right mouse button on this card, drag the arrow to a living target, then release.", "{0}: giữ chuột phải trên lá này, kéo mũi tên tới mục tiêu còn sống rồi thả để dùng kỹ năng nhé."),
("Hunter can shoot only during the Villagers' action turn.", "Hunter chỉ được nổ súng trong lượt hành động của phe Dân làng."),
("Hunter cannot shoot itself. Choose another living card on the board.", "Hunter không thể tự bắn mình. Hãy chọn một lá còn sống khác trên bàn."),
("Witch can act during an action turn or your Sacrifice voting turn, not the curse ritual.", "Witch chỉ dùng được trong lượt hành động hoặc lượt bỏ phiếu của bạn, không dùng trong Nguyền rủa."),
("Invoker can summon only during Sacrifice, when its last player is voting.", "Invoker chỉ gọi hồn trong Hiến tế, vào lượt bỏ phiếu của người đã đặt nó gần nhất."),
("Only the player who last placed Invoker can summon with it during their voting turn.", "Chỉ người đã đặt Invoker gần nhất mới được gọi hồn bằng nó, trong lượt bỏ phiếu của mình."),
("Invoker must sit next to the cemetery to summon. Move it there during an action turn.", "Invoker phải ngồi sát nghĩa trang mới gọi hồn được. Hãy đưa nó tới đó trong lượt hành động nhé."),
("The cemetery is empty. Invoker needs a dead card to summon.", "Nghĩa trang chưa có ai. Invoker cần một lá đã chết mới có thể gọi hồn."),
("Gambler accepts bets only during your Sacrifice voting turn.", "Gambler chỉ nhận đặt cược trong lượt bỏ phiếu Hiến tế của bạn."),
("Snooper normally acts automatically at the vote tally. If its active effect is available, Cursermancer selects a target during Sacrifice.", "Snooper thường tự kích hoạt khi tổng kết phiếu. Nếu có thể dùng hiệu ứng chủ động, Cursermancer chọn mục tiêu trong Hiến tế."),
("{0} has already used its skill this round. It becomes ready after the Sacrifice ritual ends.", "{0} đã dùng kỹ năng trong vòng này rồi. Kỹ năng sẽ sẵn sàng lại sau khi nghi thức Hiến tế kết thúc."),
("This skill cannot use that target now. Check its description for the required phase and target.", "Kỹ năng chưa dùng được với mục tiêu này. Bấm chuột phải xem mô tả để kiểm tra giai đoạn và mục tiêu cần chọn nhé."),
("Your turn change is being confirmed. Wait a moment before the next action.", "Đang xác nhận chuyển lượt. Chờ một chút rồi thao tác tiếp nhé."),
("Drop the card onto a seat, or into the hand area to retrieve a seated card.", "Thả bài lên ghế để đặt xuống, hoặc vào khay tay bài để thu hồi một lá đang ngồi trên bàn."),
("The board is still synchronizing. Wait for the cards to finish loading.", "Bàn chơi đang đồng bộ. Chờ các lá bài tải xong rồi thao tác nhé."),
("Choose a seated card to retrieve.", "Hãy chọn một lá đang ngồi trên bàn để thu hồi."),
("Choose a valid seat on the board.", "Hãy chọn một ghế đang có trên bàn chơi nhé."),
("The curse must follow a valid row or column from your Cursermancer. Empty seats break the path; choose a highlighted target.", "Lời nguyền đi theo hàng hoặc cột từ Cursermancer, nhưng ghế trống sẽ chặn đường. Hãy chọn một mục tiêu đang được chỉ sáng."),
("Choose a living card, not an empty seat.", "Hãy chọn một lá còn sống thay vì ghế trống nhé."),
("Preacher's shield blocks the curse on this target. Choose an unprotected card.", "Khiên của Preacher chặn lây nhiễm vào mục tiêu này. Hãy chọn một lá không được bảo vệ."),
("Purge is available only during an action turn. Finish the ritual first.", "Chỉ dùng được Thanh trừng trong lượt hành động. Hãy hoàn tất nghi thức trước nhé."),
("You have used both Purges for this match. No Purges remain.", "Bạn đã dùng hết hai lần Thanh trừng của ván này rồi."),
("Try the highlighted action first. The current instruction tells you which card and mouse button to use.", "Thử thao tác đang được chỉ sáng trước nhé. Lời hướng dẫn hiện tại sẽ chỉ bạn cần chọn lá nào và dùng nút chuột nào."),
("Preacher blocked Hunter's shot. The shot still spent its hearts and can be used again after Sacrifice.", "Preacher đã chặn phát bắn của Hunter. Phát bắn vẫn tốn tim; Hunter sẽ được bắn lại sau khi Hiến tế kết thúc."),
("Your opponent disconnected. Use Menu to leave, or wait for the connection to recover.", "Đối phương đã mất kết nối. Bạn có thể bấm Menu để rời ván, hoặc chờ kết nối trở lại."),
("Finish choosing the required Cursermancers in each card group before starting the match.", "Hãy chọn đủ Cursermancer theo yêu cầu của từng nhóm bài trước khi bắt đầu ván nhé."),
("Wait for the other player's Neutral pick before choosing your next card.", "Đang chờ đối phương chọn bài Neutral. Xong lượt của họ, bạn sẽ được chọn tiếp nhé."),
("Your chosen card is being added. Wait for the next pair before choosing again.", "Lá bạn chọn đang được đưa vào bộ bài. Chờ cặp bài tiếp theo xuất hiện rồi chọn tiếp nhé."),
]

backup = ROOT/'Backups/2026-11-01-action-feedback'
paths = ['Assets/UI/Localization/GameLocalization.asset', 'Tools/Localization/catalog.json',
         'Assets/Scenes/MainBoard.unity', 'Assets/Scenes/TutorialBoard.unity']
for rel in paths:
    dest=backup/rel
    if not dest.exists():
        dest.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(ROOT/rel,dest)
for name in ['ActionFeedback','BlockedActionFeedback']:
    ensure_meta(ROOT/f'Assets/Scripts/{name}.cs')
SCRIPTS['BlockedActionFeedback']=guid('Assets/Scripts/BlockedActionFeedback.cs')

catalog_path=ROOT/paths[0]; asset=catalog_path.read_text(encoding='utf-8-sig')
data_path=ROOT/paths[1]; data=json.loads(data_path.read_text(encoding='utf-8'))
for i,(english,vietnamese) in enumerate(translations):
    key=f'action.feedback.{i:02d}'
    entry=dict(key=key,source=english,english=english,vietnamese=vietnamese)
    block='  - key: '+json.dumps(key)+'\n'+''.join('    '+field+': '+json.dumps(entry[field],ensure_ascii=False)+'\n' for field in ['source','english','vietnamese'])
    pattern=rf'(?ms)^  - key: "?{re.escape(key)}"?\n.*?(?=^  - key:|\Z)'
    if re.search(pattern,asset): asset=re.sub(pattern,lambda _:block,asset)
    else: asset=asset.rstrip()+'\n'+block
    old=next((n for n,e in enumerate(data['entries']) if e['key']==key),None)
    if old is None: data['entries'].append(entry)
    else: data['entries'][old]=entry
catalog_path.write_text(asset,encoding='utf-8')
data_path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

for scene in ['MainBoard','TutorialBoard']:
    s=Scene(f'Assets/Scenes/{scene}.unity',True)
    ui=s.find_script(guid('Assets/Scripts/GameStartUI.cs'))
    def field(name): return int(re.search(r'^  '+name+r': \{fileID: (\d+)\}',s.records[ui],re.M)[1])
    shield=field('inputShield')
    controls={1889529060:0,800393272:1,867535428:2,shield:3}
    draft=re.search(r'  draftButtons:\n((?:  - \{fileID: \d+\}\n)+)',s.records[ui])
    if draft:
        for component in map(int,re.findall(r'fileID: (\d+)',draft[1])): controls[s.game_object(component)]=4
    for go,control in controls.items():
        comp=s.component(go,script=guid('Assets/Scripts/BlockedActionFeedback.cs'))
        if comp is None: s.mono(go,'BlockedActionFeedback',f'  control: {control}\n')
    instruction=field('instruction'); rect=s.component(s.game_object(instruction),type_=224)
    s.set(rect,'m_SizeDelta','{x: 1496, y: 42}') # two lines within the existing 96px header
    s.save()
print(f'Authored {len(translations)} bilingual explanations and saved control handlers.')

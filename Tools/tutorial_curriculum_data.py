"""Editable bilingual content for independent, playable tutorial exercises."""
from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[1]
A='Read Inspect CloseInspect Place Return BlockedReturn BlockedPlace Skill Vote EndTurn Tally Purge OpenNotes PlaceNote CloseNotes OpenCemetery CloseCemetery Curse OpenInvoker Resurrect Quiz Swap BlockedSkill'.split()
O='Place Return Hearts Phase Lock Infect Cur Kill ResetSkill Vote Curse ClosePanels Cast'.split()
def copy(en,vi):return dict(english=en,vietnamese=vi)
def op(kind,number=0,value=0,target=0):return dict(kind=O.index(kind),number=number,value=value,target=target)
def phase(value=2,voter=1):return op('Phase',value=value,target=voter)
def hearts(value=5):return op('Hearts',number=1,value=value)
def beat(action,en,vi,pEn,pVi,source=0,target=0,seat=-1,prepare=None,title=None):
    return dict(action=A.index(action),source=source,target=target,seat=seat,prepare=prepare or [],
        title=copy(*(title or ('Your next small discovery','Thêm một điều để khám phá'))),instruction=copy(en,vi),purpose=copy(pEn,pVi),
        success=copy('That is it. Keep that little clue with you.','Đúng rồi. Giữ lấy gợi ý nhỏ này nhé.'),choiceA=copy('',''),choiceB=copy('',''),answer=0)
def read(title,pEn,pVi,prepare=None):
    return beat('Read',"Press Let's continue when you are ready.",'Bấm Mình thử tiếp nhé khi bạn đã sẵn sàng.',pEn,pVi,prepare=prepare,title=title)
def quiz(en,vi,a,b,pEn,pVi):
    b0=beat('Quiz',en,vi,pEn,pVi,title=('Take a guess','Thử đoán một chút nhé'));b0['choiceA']=copy(*a);b0['choiceB']=copy(*b);return b0
def number(alias):return int(re.search(r'^  cardNumber: (\d+)',(ROOT/f'Assets/ScriptableObjects/{alias}.asset').read_text(encoding='utf-8-sig'),re.M)[1])
def place(n,seat,pEn,pVi,prepare=None):
    return beat('Place',f'Hold LEFT mouse on card number {n}, drag it to the green seat {seat+1}, then release.',
        f'Giữ CHUỘT TRÁI ở lá số {n}, kéo vào ghế số {seat+1} viền xanh rồi thả.',pEn,pVi,source=n,seat=seat,prepare=prepare,title=('Give it a place at the table','Mời một người vào bàn'))
def inspect(n):return [beat('Inspect',f'RIGHT-click card number {n}.',f'Bấm CHUỘT PHẢI vào lá số {n}.',
    'The description belongs to the live card data. Notice its timing, targets and limits before you spend anything.',
    'Đây là mô tả lấy từ chính lá bài hiện tại. Hãy xem kỹ thời điểm, mục tiêu và giới hạn trước khi dùng nhé.',source=n,title=('Get to know this card','Làm quen người bạn mới')),
    beat('CloseInspect','RIGHT-click the enlarged card again.','Bấm CHUỘT PHẢI lần nữa vào lá đang phóng to.',
    'A right-click reads a card; dragging with the right mouse aims a targeted ability. Those are two different gestures.',
    'Bấm chuột phải là xem bài; giữ và kéo chuột phải mới là nhắm kỹ năng. Hai động tác này khác nhau nhé.',source=n,title=('Back to the table','Trở về bàn chơi'))]
def vote(n,target,seat,prepare=None):return beat('Vote',f'LEFT-drag card number {n} onto card number {target}.',f'Kéo CHUỘT TRÁI từ lá số {n} sang lá số {target}.',
    'The card stays seated. This arrow is a ballot, and a card cannot vote for itself.',
    'Lá bài vẫn ngồi nguyên chỗ. Mũi tên này là phiếu bầu; một lá không được tự bỏ phiếu cho mình.',source=n,target=target,seat=seat,prepare=prepare,title=('Let one card speak','Cho một lá bài lên tiếng'))
def skill(n,target=0,prepare=None,blocked=False):return beat('BlockedSkill' if blocked else 'Skill',
    f'Hold RIGHT mouse on card number {n}, drag to card number {target}, then release.' if target else f'LEFT-click card number {n} to use its ability.',
    f'Giữ CHUỘT PHẢI ở lá số {n}, kéo sang lá số {target} rồi thả.' if target else f'Bấm CHUỘT TRÁI vào lá số {n} để dùng kỹ năng.',
    'Watch the board and the feedback: success, a shield and a silenced ability are different outcomes.',
    'Nhìn cả bàn chơi lẫn lời phản hồi nhé: dùng thành công, bắn trúng khiên và bị khóa kỹ năng là những kết quả khác nhau.',source=n,target=target,prepare=prepare,title=('Try it with your own hand','Tự tay thử một lần'))
def end():return beat('EndTurn','Press End turn near the cards.','Bấm Kết thúc lượt ở gần các lá bài.',
    'You may stop with hearts left. The other side gets its turn; your cards need time before they can be freely moved.',
    'Bạn có thể kết thúc khi còn tim. Đến lượt phía bên kia; những lá vừa đặt cần chờ đúng lượt mới được rút tự do.',title=('Pass the lantern','Chuyền ngọn đèn sang bên kia'))
def tally():return beat('Tally','Press Confirm votes, then watch the resolution.','Bấm Chốt phiếu, rồi quan sát tổng kết.',
    'Original ballots appear first, then the real card effects. The highest final count loses; tied highest counts all lose unless a verdict skill overrides it.',
    'Phiếu gốc hiện trước, rồi kỹ năng thật mới can thiệp. Lá nhiều phiếu cuối cùng bị hiến tế; hòa cao nhất thì cùng bị loại, trừ kỹ năng đổi phán quyết.',title=('Hear the whole village','Nghe cả ngôi làng lên tiếng'))

lessons=[]
BASE=['Villager1','Villager2','Villager3','Villager9','Villager10','Villager11','Villager12','Villager13']
def lesson(id,chapter,title,summary,beats,setup=None,focus=None):
    cards=list(BASE)
    if focus:
        cards=[c for c in cards if number(c)!=number(focus)]+[focus]
    item=dict(id=id,chapter=chapter,title=copy(*title),summary=copy(*summary),cards=cards,
        setup=[phase(),hearts(),op('Hearts',value=5)]+(setup or []),beats=beats)
    lessons.append(item);return item

lesson('controls-first-night',0,('Your first cards','Những lá bài đầu tiên'),
    ('Read, place, understand a movement lock, then retrieve a card.','Xem bài, đặt bài, hiểu thời gian khóa rồi tự rút bài về.'),[
    read(('Come closer to the table','Lại gần bàn chơi nhé'),'The seven seats are the village table. Your hand is below it. You will learn one gesture at a time, with no opponent rushing you.',
         'Bảy chiếc ghế là bàn của ngôi làng; tay bài nằm phía dưới. Mình thử từng động tác một thôi, không có đối thủ nào giục bạn cả.'),
    *inspect(10),place(10,0,'Placing a card from your printed faction spends 2 hearts. Watch the five-heart bar become three.',
        'Đặt một lá thuộc nhóm gốc của phe mình tốn 2 tim. Nhìn thanh năng lượng từ 5 còn 3 tim nhé.'),
    beat('BlockedReturn','Try to LEFT-drag the card you just placed back to your hand.','Thử kéo CHUỘT TRÁI lá vừa đặt trở về tay bài.',
        'This refusal is useful: a newly placed card is committed. Read the named lock message; it cannot immediately be taken back.',
        'Lời từ chối này rất đáng nhớ: lá vừa đặt đã cam kết ở lại bàn. Đọc thông báo khóa của lá đó nhé; bạn không thể rút lại ngay.',source=10,title=('A decision needs time','Một nước đi cần thời gian')),
    end(),end(),beat('Return','Now LEFT-drag card number 10 back into your hand.','Bây giờ kéo CHUỘT TRÁI lá số 10 trở lại tay bài.',
        'After the other side has acted and your next action turn arrives, this ordinary card is unlocked. Returning your own card refunds the hearts actually paid, up to the limit.',
        'Khi phía bên kia đã chơi và tới lượt hành động kế tiếp của bạn, lá thường này hết khóa. Rút bài mình hoàn lại số tim đã trả, tối đa bằng giới hạn năng lượng.',source=10,title=('Welcome it home','Đón lá bài trở về'))])

lesson('controls-hunter',0,('Hunter: aim, do not just click','Hunter: nhắm bắn, không chỉ bấm'),
    ('Use a real shot, distinguish right-click from right-drag, and visit the cemetery.','Tự bắn một phát, phân biệt bấm/kéo chuột phải và xem nghĩa trang.'),[
    read(('A steady hand','Giữ tay thật vững'),'Hunter is already seated. Five hearts buy one shot; the ability refreshes after Sacrifice, not at every new action turn.',
        'Hunter đã ngồi sẵn. Một phát bắn dùng đủ 5 tim; kỹ năng hồi sau khi xong hiến tế, không phải cứ sang lượt mới là được bắn lại.'),
    skill(9,1),beat('OpenCemetery','Open Cemetery.','Bấm Nghĩa trang.','The shot removed a real card. Dead cards cannot sit or vote until a resurrection brings them back.',
        'Phát bắn đã loại một lá thật. Bài chết không được ngồi hay bỏ phiếu, trừ khi có kỹ năng hồi sinh.',title=('Follow the consequence','Nhìn lại kết quả')),
    beat('CloseCemetery','Close Cemetery with the same button.','Bấm lại Nghĩa trang để đóng.','Reading the cemetery costs no hearts. Return to the table when you have seen the result.',
        'Xem nghĩa trang không tốn tim. Khi đã thấy kết quả, mình trở về bàn nhé.'),
    quiz('Can Hunter fire again just because the next action turn begins?','Chỉ sang lượt hành động mới, Hunter đã bắn lại được chưa?',('No · wait for Sacrifice','Chưa · chờ xong hiến tế'),('Yes · every action turn','Được · mỗi lượt một lần'),
        'A powerful shot has a round cooldown. A shield can block the kill while still consuming the shot and its hearts.',
        'Phát bắn mạnh nên có thời gian hồi theo vòng. Khiên có thể cứu mục tiêu, nhưng phát bắn và số tim vẫn bị tiêu hao.')],
    setup=[op('Place',9,1),op('Place',1,2),op('Cur',1)])

lesson('controls-ritual',0,('From a curse to a sacrifice','Từ lời nguyền tới hiến tế'),
    ('Try the infection gesture, cast individual votes and watch a real tally.','Thử lây nhiễm, bỏ từng phiếu rồi xem tổng kết thật.'),[
    read(('A quieter kind of danger','Một mối nguy lặng lẽ'),'For this short exercise you see both sides. Infection follows occupied seats along a row or column; an empty seat breaks that path.',
        'Ở bài ngắn này, bạn thử cả hai phía. Lây nhiễm đi qua các ghế có người cùng hàng hoặc cột; ghế trống sẽ cắt đường lây.'),
    beat('Curse','LEFT-drag card number 1 onto card number 11.','Kéo CHUỘT TRÁI từ lá số 1 sang lá số 11.',
        'Number 11 is the nearest eligible card in that direction. Infection changes its status; it does not create a new Cursermancer. Already infected cards may be skipped, but empty seats stop the path.',
        'Lá 11 là mục tiêu hợp lệ gần nhất theo hướng đó. Nhiễm bệnh không tạo thêm Cursermancer. Lá đã nhiễm có thể được bỏ qua, nhưng ghế trống chặn đường lây.',source=1,target=11,seat=1,title=('Leave one trace','Để lại một dấu vết')),
    beat('EndTurn','Press the button to enter Sacrifice.','Bấm nút để sang Hiến tế.','Both factions vote in order. A card votes with its printed faction even if it secretly carries a different role.',
        'Hai phe lần lượt bỏ phiếu. Lá bài bỏ phiếu theo nhóm gốc in trên bài, kể cả khi nó đang giấu một vai trò khác.'),
    vote(10,1,2,prepare=[phase(4),op('Vote',1,0)]),vote(11,1,2),tally(),
    read(('Numbers become a consequence','Số phiếu dẫn tới một kết cục'),'You made two cards vote separately. At the tally both sides became public. Neutral cards do not cast normal faction ballots, but their abilities can alter the outcome.',
        'Bạn vừa cho hai lá bỏ phiếu riêng. Khi tổng kết, phiếu hai phía mới công khai. Bài Neutral không tự bỏ phiếu phe, nhưng kỹ năng của chúng có thể đổi kết quả.')],
    setup=[op('Place',10,0),op('Place',11,1),op('Place',1,2),op('Cur',1),phase(3,0)])

lesson('controls-purge',0,('Purge: what is actually removed?','Thanh trừng: lá nào thật sự bị loại?'),
    ('Try a Purge on a prepared infection, including a card in hand.','Thử thanh trừng bệnh đã chuẩn bị, kể cả lá đang ở trên tay.'),[
    read(('The danger can come home','Mầm bệnh có thể theo về tay'),'Some infected cards are already back in hand. Purge checks infections across the board and hands; it does not kill someone merely for being a Cursermancer.',
        'Có lá nhiễm bệnh đã trở về tay. Thanh trừng tìm bệnh cả trên bàn lẫn trong tay; nó không loại một lá chỉ vì lá đó là Cursermancer.'),
    beat('Purge','Press Purge.','Bấm Thanh trừng.','Each side has only two Purges for the whole match. This prepared infection makes the result safe to observe.',
        'Mỗi phe chỉ có hai lần thanh trừng trong cả ván. Bệnh được chuẩn bị sẵn để bạn yên tâm quan sát kết quả.',source=10,title=('Spend one scarce chance','Dùng một cơ hội quý')),
    quiz('Did this reveal the Cursermancer directly?','Thanh trừng có chỉ thẳng Cursermancer không?',('No · it reveals infections','Không · nó tìm bệnh'),('Yes · it finds the role','Có · nó tìm vai trò'),
        'A victim is a clue to a past path, not proof of the source identity. The deduction chapter will help you use that clue.',
        'Nạn nhân gợi lại đường lây trước đó, chưa phải bằng chứng về danh tính nguồn lây. Phần suy luận sẽ giúp bạn dùng dấu vết này.')],setup=[op('Infect',10,1),op('Cur',1)])

lesson('energy-empty',1,('Five hearts, one small plan','Năm tim, một kế hoạch nhỏ'),
    ('Spend hearts, try an unaffordable placement, and see a real turn refill.','Tiêu tim, thử một nước không đủ năng lượng rồi sang lượt để hồi tim.'),[
    read(('Hearts are your action budget','Tim là sức để hành động'),'These hearts are energy, not health. The number beside them tells you how much of this turn you can still spend.',
        'Tim ở đây là năng lượng, không phải máu. Con số bên cạnh cho biết trong lượt này bạn còn đủ sức làm bao nhiêu việc.'),
    place(10,0,'Your own printed faction costs 2 hearts to place. Five becomes three.','Bài thuộc nhóm gốc của phe mình tốn 2 tim để đặt. Từ 5 còn 3 tim.'),
    place(11,1,'Another ordinary placement spends two more. Now you have one heart left.','Đặt thêm một lá thường dùng 2 tim nữa. Bây giờ bạn còn 1 tim.'),
    beat('BlockedPlace','Try to place card number 12 in seat 3.','Thử đặt lá số 12 vào ghế số 3.',
        'You have one heart, but this placement needs two. The refused attempt spends nothing. Read the message instead of guessing whether the mouse failed.',
        'Bạn còn 1 tim nhưng nước đặt này cần 2. Thử thất bại không mất thêm tim. Đọc thông báo nhé, không phải chuột của bạn bị lỗi.',source=12,seat=2,title=('One heart cannot pay for two','Một tim chưa đủ trả hai')),
    end(),end(),quiz('How many hearts start your new action turn?','Lượt hành động mới của bạn bắt đầu với bao nhiêu tim?',('Five · a fresh budget','5 tim · năng lượng mới'),('Six · keep the spare heart','6 tim · cộng tim thừa'),
        'A new action turn restores the pool to five. Unspent hearts are not saved on top of the limit.',
        'Lượt hành động mới hồi năng lượng về 5 tim. Tim chưa dùng không được cộng dồn vượt giới hạn.')])

lesson('energy-other-side',1,('An expensive visitor','Một vị khách tốn nhiều sức'),
    ('Place an opposing card, reach zero, and understand why another action is refused.','Đặt bài khác phe, dùng hết tim và hiểu vì sao không làm tiếp được.'),[
    read(('The printed group changes the price','Nhìn nhóm bài trước khi trả giá'),'You are playing Village. Card number 3 is from the other printed faction. A card placed from the opposing or Neutral hand normally costs all five hearts.',
        'Bạn đang chơi Village; lá số 3 thuộc nhóm gốc phía bên kia. Đặt bài từ tay đối phương hoặc Neutral thường dùng trọn 5 tim.'),
    place(3,0,'Watch all five hearts go dark. One placement can be the whole turn.','Nhìn cả 5 tim tắt nhé. Chỉ một nước đặt cũng có thể dùng hết lượt.'),
    beat('BlockedPlace','Try to place card number 10 in seat 2.','Thử đặt lá số 10 vào ghế số 2.',
        'At zero hearts you cannot pay even the usual two-heart price. Refused actions keep the board and budget unchanged.',
        'Khi còn 0 tim, bạn chưa trả nổi cả giá 2 tim thông thường. Nước bị từ chối giữ nguyên bài và năng lượng.',source=10,seat=1,title=('A quiet moment at zero','Khi năng lượng về không')),
    quiz('Can you retrieve an opposing card for a free refund?','Rút bài đối phương có được hoàn tim miễn phí không?',('No · it costs five hearts','Không · phải trả 5 tim'),('Yes · every return refunds','Có · rút gì cũng hoàn'),
        'Retrieving your own or a Neutral card refunds the amount it actually paid, up to two. Retrieving an opposing card charges five instead. Movement locks still apply.',
        'Rút bài mình hoặc Neutral hoàn khoản đã trả, tối đa 2 tim. Rút bài đối phương phải trả 5 tim. Các giới hạn khóa di chuyển vẫn áp dụng.')])

lesson('energy-movement',1,('Moving is not placing again','Di chuyển khác với đặt mới'),
    ('Compare one-heart and three-heart movement on the same table.','So sánh di chuyển 1 tim và 3 tim ngay trên bàn.'),[
    read(('A shorter journey','Một quãng đường ngắn hơn'),'These cards were seated before this exercise. Moving your own or a Neutral card costs one heart; moving an opposing card costs three.',
        'Các lá này đã ngồi từ trước bài tập. Dời bài mình hoặc Neutral tốn 1 tim; dời bài đối phương tốn 3 tim.'),
    place(10,1,'Move an already seated Village card: five hearts becomes four.','Dời một lá Village đã ngồi trên bàn: từ 5 còn 4 tim.'),
    place(3,4,'Move the opposing card: four becomes one. A short visual move can still be expensive.','Dời lá khác phe: từ 4 còn 1 tim. Khoảng cách nhìn ngắn nhưng giá vẫn khác.'),
    quiz('Does dragging farther across the table cost extra hearts?','Kéo xa hơn trên bàn có làm tốn thêm tim không?',('No · faction and action matter','Không · tùy nhóm và thao tác'),('Yes · distance sets the price','Có · càng xa càng đắt'),
        'Price follows the action and printed affiliation, not pixel distance. Totem and Prankster have special prices explained in their own lessons.',
        'Giá theo thao tác và nhóm gốc, không theo số pixel bạn kéo. Totem và Prankster có giá riêng, được giải thích ở bài của chúng.')],setup=[op('Place',10,0),op('Place',3,5)])

lesson('energy-swap',1,('A swap spends one shared price','Đổi chỗ trả một khoản chung'),
    ('Swap a hand card with an opposing seated card and reach zero.','Đổi bài trên tay với bài đối phương trên bàn rồi nhìn tim về 0.'),[
    read(('Make room by changing places','Nhường chỗ bằng một lần đổi'),'Card number 3 is already unlocked. You will replace it with number 12 from your hand. A hand swap involving an opposing card costs five hearts in total.',
        'Lá số 3 đã hết khóa. Bạn sẽ đổi nó với lá số 12 từ tay. Đổi từ tay có dính bài đối phương tốn tổng cộng 5 tim.'),
    beat('Swap','LEFT-drag card number 12 onto card number 3.','Kéo CHUỘT TRÁI lá số 12 thả lên lá số 3.',
        'The seated card returns to its hand while yours takes its seat. This is one swap price, not a placement plus a second retrieval bill.',
        'Lá trên ghế về đúng tay của nó, còn lá của bạn chiếm chỗ. Đây là một giá đổi chung, không cộng riêng tiền đặt và tiền rút.',source=12,target=3,seat=0,title=('Trade places','Thử một lần đổi chỗ')),
    quiz('Can you immediately make another paid move at zero hearts?','Về 0 tim rồi, bạn còn trả được một nước mất tim nữa không?',('No · end turn or a valid refund','Không · đổi lượt hoặc hoàn hợp lệ'),('Yes · swaps do not spend energy','Có · đổi chỗ không tốn sức'),
        'Own/Neutral hand swaps normally cost three; opposing involvement costs five. Board-only swaps cost two, three or five depending on how many opposing cards are involved.',
        'Đổi từ tay giữa bài mình/Neutral thường tốn 3; dính đối phương tốn 5. Đổi giữa hai lá trên bàn tốn 2, 3 hoặc 5 tùy có bao nhiêu lá đối phương.')],setup=[op('Place',3,0)])

# Each card gets its own small lab. Full, current card text is inspected in-game.
studies=[
('Trader','Trader20','It replaces the occupied horizontal group counts with equal shares of the highest count. Extra votes and penalties happen first; empty seats do not enter the divisor.',
 'Trader lấy số phiếu cao nhất trong nhóm ngang rồi chia cho các lá có mặt. Cộng/trừ phiếu xảy ra trước; ghế trống không tính vào số chia.',
 ('Round down; discard the remainder','Làm tròn xuống, bỏ phần dư'),('Keep every leftover vote','Giữ lại mọi phiếu dư')),
('Jester','Jester20','Its tally roll can fail. When it fires, exactly three extra votes are scattered among other living seated cards. The animation follows the actual roll.',
 'Jester có thể không kích hoạt. Khi kích hoạt, đúng 3 phiếu được tung vào những lá sống khác trên bàn. Hiệu ứng đi theo kết quả thật.',
 ('Random; sometimes no extra votes','Ngẫu nhiên; đôi khi không có'),('Three votes for your chosen target','3 phiếu vào mục tiêu bạn chọn')),
('Snooper','Snooper22','Healthy Snooper adds one vote to a living seated Cursermancer at the tally. The indicated target becomes public only when the vote effect resolves.',
 'Snooper khỏe cộng 1 phiếu vào một Cursermancer sống trên bàn khi tổng kết. Mục tiêu được công khai lúc hiệu ứng phiếu được phân giải.',
 ('Public when the tally resolves','Công khai lúc tổng kết'),('Always public before voting ends','Luôn lộ trước khi chốt phiếu')),
('Gambler','Gambler24','The first bettor wins only if the second does not bet. The second bettor wins only if both bet. Winning adds a vote to the bet target; losing removes a vote from that player normal ballot target if possible.',
 'Người cược trước thắng nếu người sau không cược; người cược sau thắng nếu cả hai cược. Thắng cộng phiếu vào mục tiêu cược; thua trừ phiếu ở mục tiêu lá phiếu gốc của người đó nếu còn phiếu.',
 ('The second bettor wins','Người cược sau thắng'),('Both bettors win','Cả hai người cùng thắng')),
('Controller','Controller22','Controller changes the verdict to the lowest positive vote count. It does not erase or rewrite the shown ballot totals. Sacrifier has higher override priority.',
 'Controller đổi án sang nhóm có số phiếu dương thấp nhất. Nó không xóa hay viết lại tổng phiếu đang hiển thị. Sacrifier có quyền ưu tiên cao hơn.',
 ('The lowest positive count','Số phiếu dương thấp nhất'),('Every card with zero votes','Mọi lá có 0 phiếu')),
('Sacrifier','Sacrifier21','When its override is active, Sacrifier becomes the sacrifice itself. The vote totals remain evidence, but no longer choose the victim. It overrides Controller.',
 'Khi giành quyền phán quyết, Sacrifier tự nhận hiến tế. Tổng phiếu vẫn giữ để bạn đối chiếu nhưng không còn chọn nạn nhân. Nó ưu tiên hơn Controller.',
 ('Sacrifier takes the sacrifice','Sacrifier tự chịu hiến tế'),('Controller always outranks it','Controller luôn ưu tiên hơn')),
('Preacher','Preacher18','Its horizontal neighbors gain shields. A shield blocks a Hunter kill, but the successful shot still spends its hearts and cooldown. Shields are established before reactive pushes.',
 'Hai hàng xóm ngang được khiên. Khiên chặn cú giết của Hunter, nhưng phát bắn đã dùng vẫn mất tim và hồi chiêu. Khiên có sẵn trước các cú đẩy phản ứng.',
 ('The shot still spends its cost','Phát bắn vẫn tiêu hao'),('The blocked shot is free','Bắn vào khiên là miễn phí')),
('Elder','Elder17','Its horizontal neighbors are locked against manual movement. This is an aura lock, different from the temporary lock after you place an ordinary card.',
 'Elder khóa di chuyển thủ công của hàng xóm ngang. Đây là khóa hào quang, khác với thời gian chờ sau khi vừa đặt một lá thường.',
 ('Remove the aura before moving','Gỡ hào quang trước khi dời'),('Waiting one turn always unlocks it','Chờ một lượt là luôn mở khóa')),
('Saboteur','Saboteur18','Its horizontal neighbors cannot use their original card abilities. The Cursermancer infection ability is separate and remains available.',
 'Saboteur chặn kỹ năng gốc của hàng xóm ngang. Khả năng lây nhiễm của vai trò Cursermancer là riêng và vẫn dùng được.',
 ('Only the printed card ability','Chỉ kỹ năng gốc của lá'),('Also every Cursermancer infection','Chặn luôn mọi lây nhiễm')),
('GameMaster','Master21','It creates extra seats. Occupied extra seats prevent manually moving GameMaster, but card pushes move the seats and occupants together. Returning it returns those occupants; its death kills them even through Preacher shields.',
 'GameMaster tạo ghế phụ. Có người trên đó thì không dời tay GameMaster được, nhưng kỹ năng đẩy vẫn mang ghế và bài theo. Về tay thì cùng về; chết thì bài ghế phụ chết theo, kể cả có khiên Preacher.',
 ('Card effects can still push it','Kỹ năng khác vẫn đẩy được'),('Nothing can ever move it','Không gì có thể di chuyển nó')),
('Prankster','Prankster24','A card in its left trigger area can be pushed whenever the area is evaluated. On the first placement there is a ten-percent return-to-hand chance. A normal push chooses an empty main seat.',
 'Có lá trong vùng kích hoạt bên trái thì Prankster có thể đẩy khi vùng được xét. Lúc mới đặt có 10% đẩy về tay; cú đẩy thường chọn ghế chính còn trống.',
 ('A card stays in the trigger area','Có lá nằm trong vùng kích hoạt'),('Only clicking Prankster again','Phải bấm Prankster lần nữa')),
('VillageChief','Reeve17','When an eligible ordinary faction card is present, VillageChief can end the action buildup and call the ritual early. A prepared round can change before the seven main seats are all filled.',
 'Khi có lá phe thường đủ điều kiện, VillageChief có thể gọi nghi thức sớm. Vòng chơi có thể đổi trước khi đủ bảy ghế chính.',
 ('A ritual can arrive early','Nghi thức có thể đến sớm'),('All seven seats must always fill','Luôn phải đủ cả bảy ghế')),
('Totem','Totem1','An unsilenced Totem uses its special one-heart placement and retrieval price. Silence removes that special discount; its printed faction still matters for ordinary rules.',
 'Totem chưa bị câm lặng dùng giá đặt/rút riêng 1 tim. Bị khóa kỹ năng thì mất giảm giá; nhóm gốc của nó vẫn áp dụng cho các luật thường.',
 ('Saboteur removes the discount','Saboteur làm mất giảm giá'),('Its discount ignores silence','Giảm giá bất chấp câm lặng')),
('Blind','Blind19','Blind checks one newest uninspected death when eligible, once per cycle. Repeated aura refreshes do not re-read the same death. Decay can falsify the public report; Cursermancer still sees the truth.',
 'Khi đủ điều kiện, Blind xem một lần chết mới nhất chưa xem, mỗi chu kỳ một lần. Cập nhật hào quang không soi lại cùng lần chết. Decay có thể báo sai; Cursermancer vẫn thấy sự thật.',
 ('No; an inspected death stays read','Không; lần chết đó đã được xem'),('Yes; every refresh re-reads it','Có; cập nhật là soi lại')),
('Detective','Detactive23','With both horizontal neighbors occupied, it adds one suspicion to the more suspicious neighbor. Suspicion is a clue used for later comparisons, not an extra ballot.',
 'Khi đủ hai hàng xóm ngang, Detective thêm 1 nghi ngờ vào lá đáng ngờ hơn. Nghi ngờ dùng để so sánh về sau, không phải phiếu bầu cộng thêm.',
 ('Suspicion; not an extra vote','Nghi ngờ; không phải phiếu'),('One automatic extra ballot','Tự động thêm một phiếu')),
('Witch','Witch23','A click spends five hearts and removes other infected cards on the board. Witch excludes itself and infected cards still in hand. Its ability refreshes after the sacrifice finishes.',
 'Một lần bấm dùng 5 tim để loại những lá nhiễm khác trên bàn. Witch không tự loại mình và không quét bài còn trên tay. Kỹ năng hồi sau khi xong hiến tế.',
 ('Other infected seated cards','Các lá nhiễm khác trên bàn'),('Every infected card in every hand','Mọi lá nhiễm trong mọi tay')),
('Invoker','Invoker19','During Sacrifice its last placer can use it beside the cemetery. Opening the choices does not consume the use; selecting a resurrection does. Resurrected hidden roles retain their identity.',
 'Trong Hiến tế, người đặt cuối có thể dùng Invoker cạnh nghĩa trang. Mở lựa chọn chưa tiêu hao lượt dùng; chọn hồi sinh mới tiêu hao. Vai trò bí mật của lá hồi sinh vẫn giữ.',
 ('Only after choosing a card','Sau khi chọn lá hồi sinh'),('As soon as choices are opened','Ngay khi vừa mở lựa chọn')),
]

for name,alias,en,vi,answer,wrong in studies:
    n=number(alias);baseCur=2 if n==1 else 1
    setup=[op('Place',baseCur,0),op('Place',10,2)]
    focusSeat=6 if name in ['Blind','Invoker'] else 1
    if name=='Blind':setup+=[op('Kill',10)]
    if name=='Invoker':setup+=[op('Kill',10)]
    if name=='Prankster':setup=[op('Place',baseCur,0),op('Place',10,1)];focusSeat=2
    if name=='GameMaster':setup=[op('Place',baseCur,0)]
    if name=='Preacher':setup+=[op('Place',9,4)]
    if name=='Saboteur':setup=[op('Place',9,0),op('Place',10,2)]
    if name=='Witch':setup+=[op('Infect',baseCur,1),op('Infect',11,1)]
    if name in ['Trader','Controller']:setup+=[op('Place',2,3),op('Place',3,4)]
    if name=='Snooper':setup+=[op('Cur',baseCur)]
    steps=[read((f'Meet {name}',f'Làm quen {name}'),en,vi),*inspect(n),place(n,focusSeat,
        f'This is a separate practice board for {name}. Its live ability uses the same rules as a match.',
        f'Đây là bàn luyện riêng cho {name}. Kỹ năng thật của lá vẫn dùng đúng luật như trong trận.')]
    if name in ['Trader','Jester','Snooper','Gambler','Controller','Sacrifier']:
        prep=[phase(4),op('Vote',baseCur,2)]
        if name in ['Trader','Controller']:prep+=[op('Vote',2,2),op('Vote',3,2)]
        if name=='Gambler':
            steps+=[skill(n,10,prepare=[phase(4,0)])]
        steps+=[vote(10,baseCur,0,prepare=prep),tally()]
    elif name=='Preacher':steps+=[skill(9,baseCur,prepare=[hearts()])]
    elif name=='Elder':steps+=[beat('BlockedReturn','Try to LEFT-drag number 10 back to hand.','Thử kéo CHUỘT TRÁI lá số 10 về tay.',en,vi,source=10)]
    elif name=='Saboteur':steps+=[skill(9,10,blocked=True)]
    elif name=='GameMaster':
        steps+=[place(10,7,'New mini-round: five hearts are prepared so you can occupy an extra seat.',
            'Tình huống nhỏ tiếp theo đã chuẩn bị 5 tim để bạn thử ngồi ghế phụ.',prepare=[hearts()]),
            beat('BlockedReturn','Try to LEFT-drag GameMaster back to hand.','Thử kéo CHUỘT TRÁI GameMaster về tay.',en,vi,source=n)]
    elif name=='Witch':steps+=[skill(n,prepare=[hearts()])]
    elif name=='Invoker':
        steps+=[beat('OpenInvoker',f'LEFT-click Invoker (number {n}).',f'Bấm CHUỘT TRÁI vào Invoker (số {n}).',en,vi,source=n,prepare=[phase(4)]),
            beat('Resurrect','Choose the offered Villager to resurrect.','Chọn lá Villager được đưa ra để hồi sinh.',en,vi,target=10)]
    elif name=='Blind':
        steps+=[beat('OpenCemetery','Open Cemetery and look at the inspected card.','Mở Nghĩa trang, nhìn lá vừa được Blind kiểm tra.',en,vi),
                beat('CloseCemetery','Close Cemetery to continue.','Đóng Nghĩa trang để tiếp tục.',en,vi)]
    else:steps+=[read(('See what actually changed','Nhìn điều vừa thay đổi'),en,vi)]
    question={'Trader':('What happens to indivisible votes?','Phiếu không chia hết thì sao?'),
        'Gambler':('If both players bet, who wins?','Nếu cả hai người cùng cược, ai thắng?'),
        'Blind':('Will another aura refresh re-read that same death?','Cập nhật hào quang có soi lại cùng lần chết đó không?')}.get(name,
        (f'Which statement fits {name}?',f'Điều nào đúng với {name}?'))
    steps+=[quiz(*question,answer,wrong,en,vi)]
    lesson('card-'+name.lower(),2,(name+' · a small laboratory',name+' · một bàn thử riêng'),
        (f'See {name} work on a real practice board; learn its timing and limits.',f'Xem {name} hoạt động trên bàn thật, nắm thời điểm và giới hạn.'),steps,setup=setup,focus=alias)

lesson('card-snooper-decay',2,('Snooper · the hidden Decay choice','Snooper · lựa chọn Decay bí mật'),
    ('Choose a private target, then watch it become public at the tally.','Chọn mục tiêu bí mật rồi xem nó công khai lúc tổng kết.'),[
    read(('A different whisper','Một lời thì thầm khác'),'This Snooper is infected. In this exercise you briefly control Cursermancer to choose its Decay target, then return to Village for the tally.',
        'Snooper này đã nhiễm. Bạn thử phía Cursermancer một chút để chọn mục tiêu Decay, rồi trở về Village xem tổng kết.'),
    skill(22,10),quiz('Can Village see this target or the used-skill mark now?','Village đã thấy mục tiêu hay dấu dùng kỹ năng chưa?',('No · only Cursermancer','Chưa · chỉ Cursermancer'),('Yes · both players see it','Rồi · cả hai phía đều thấy'),
        'The target and usage stay private until resolution. At the tally the actual indicated target becomes public, just like healthy Snooper.',
        'Mục tiêu và dấu dùng chiêu được giấu tới lúc phân giải. Khi tổng kết, mục tiêu thật mới công khai, giống Snooper khỏe.'),
    vote(10,1,0,prepare=[phase(4),op('Vote',1,2)]),tally()],
    setup=[op('Place',1,0),op('Place',22,1),op('Place',10,2),op('Infect',22,1),phase(4,0)],focus='Snooper22')

lesson('card-gambler-counter',2,('Gambler · answering a hidden bet','Gambler · đối đáp một lần cược'),
    ('Both players bet; observe the first loss and second win separately.','Cả hai cùng cược; xem riêng người trước thua và người sau thắng.'),[
    read(('Do they dare answer?','Họ có dám đáp lại không?'),'The first player already made a hidden bet. You are the second player in this prepared example. Choosing to bet answers their wager.',
        'Người đi trước đã cược bí mật. Trong ví dụ chuẩn bị này bạn là người sau. Cược lúc này là đáp lại nước cược của họ.'),
    skill(24,1),vote(10,1,0),tally(),quiz('With both bets placed, which outcome belongs to the first player?','Cả hai cùng cược, người đi trước nhận kết quả nào?',('Lose · remove a normal ballot','Thua · trừ phiếu gốc'),('Win · add a target ballot','Thắng · cộng phiếu cược'),
        'The second bettor wins when both bet. Each result has its own row and animation; a losing effect cannot remove a vote from an already empty count.',
        'Khi cả hai cược, người sau thắng. Mỗi kết quả có dòng và hiệu ứng riêng; không thể trừ thêm nếu mục tiêu đã không còn phiếu.')],
    setup=[op('Place',1,0),op('Place',24,1),op('Place',10,2),phase(4,0),op('Cast',24,target=10),op('Vote',1,2),phase(4)],focus='Gambler24')

lesson('strategy-notes',3,('A notebook, not an oracle','Sổ tay để nhớ, không phải để đoán hộ'),
    ('Open the seven-seat notebook and record a position yourself.','Mở sổ đủ bảy ghế và tự ghi lại một vị trí.'),[
    read(('Memory likes a little help','Trí nhớ cũng cần bạn đồng hành'),'The notebook is your private memory. It does not know who the Cursermancer is, and a note does not change a card or reveal a secret.',
        'Sổ tay là trí nhớ riêng của bạn. Nó không biết ai là Cursermancer; ghi chú không đổi lá bài hay tự tiết lộ bí mật.'),
    beat('OpenNotes','Open Notes.','Bấm Sổ tay.','Each of the seven slots represents one main seat. Keep the seat number with your observation so a later Purge can mean something.',
        'Bảy ô ứng với bảy ghế chính. Ghi kèm số ghế để về sau kết quả thanh trừng còn giúp bạn suy luận.'),
    beat('PlaceNote','Drag a note token into slot 7.','Kéo một mảnh ghi chú vào ô số 7.',
        'Try the last slot too. A note is your own record; if you are unsure, treat it as a question rather than a confirmed identity.',
        'Thử cả ô cuối nhé. Đây là ghi nhớ của riêng bạn; khi chưa chắc, hãy xem nó là câu hỏi chứ đừng vội chốt danh tính.',seat=6),
    beat('CloseNotes','Close Notes with its button.','Bấm lại Sổ tay để đóng.','Your note stays while you return to the board. Come back to it after observing the ritual or a Purge.',
        'Ghi chú vẫn ở đó khi bạn trở về bàn. Hãy mở lại sau khi quan sát nghi thức hoặc thanh trừng.')])

lesson('strategy-purge-trace',3,('Read the path a Purge leaves behind','Đọc đường lây từ một lần thanh trừng'),
    ('Remember last ritual seats, Purge an infected returnee, and narrow the source positions.','Nhớ ghế ở nghi thức trước, thanh trừng lá đã về tay rồi khoanh vùng nguồn lây.'),[
    read(('Look before the chairs empty','Nhìn trước khi ghế trống'),'Last ritual snapshot: number 12 sat in seat 1, number 10 in seat 2, and number 1 in seat 3. Seat 4 was empty. Remember this small occupied chain.',
        'Bàn ở nghi thức trước: lá 12 ngồi ghế 1, lá 10 ghế 2, lá 1 ghế 3. Ghế 4 trống. Nhớ chuỗi ghế có người này nhé.'),
    beat('OpenNotes','Open Notes before the cards return.','Mở Sổ tay trước khi bài trở về.',
        'A later Purge names a victim, not its old seat. Writing positions now preserves the part the returned hand no longer shows.',
        'Thanh trừng sau này chỉ ra nạn nhân, không chỉ ghế cũ. Ghi vị trí bây giờ giúp bạn giữ lại phần thông tin mà tay bài không còn cho thấy.'),
    beat('PlaceNote','Put a memory token into slot 2.','Đặt một mảnh ghi nhớ vào ô số 2.','You are recording the victim candidate old position. The notebook does not mark it guilty.',
        'Bạn đang ghi vị trí cũ của một lá có thể thành nạn nhân. Sổ không kết luận lá đó có tội.',seat=1),
    beat('CloseNotes','Close Notes to see what the next turn reveals.','Đóng Sổ tay để xem lượt tiếp theo mang lại gì.',
        'The prepared replay uses a real curse and returns the cards to hand. You will stay on the Village view.',
        'Tình huống phát lại dùng một lần lây thật rồi đưa bài về tay. Bạn vẫn xem từ phía Village.'),
    beat('Purge','Press Purge on this new action turn.','Bấm Thanh trừng trong lượt hành động mới này.',
        'Number 10 is infected in hand. When it is removed, use your seat memory to ask where its source could have reached it from.',
        'Lá số 10 đang nhiễm trong tay. Khi nó bị loại, dùng vị trí đã nhớ để hỏi nguồn lây có thể chạm tới nó từ đâu.',
        prepare=[op('Cur',1),op('Curse',1,1),phase(),op('Return',1),op('Return',10),op('Return',12)]),
    quiz('Which old positions remain candidates along the shown occupied chain?','Theo chuỗi ghế đã thấy, vị trí cũ nào còn đáng xét?',('Seats 1 or 3 · not proof yet','Ghế 1 hoặc 3 · chưa kết luận'),('Seat 7 · distance does not matter','Ghế 7 · khoảng cách không quan trọng'),
        'The victim sat in seat 2. Occupied row/column paths can connect a source; the empty seat 4 breaks this row. It narrows candidates, not one certain identity. Extra seats and other clues can change the candidate set.',
        'Nạn nhân từng ở ghế 2. Đường có người cùng hàng/cột nối được nguồn lây; ghế 4 trống cắt hàng này. Đó là khoanh vùng, chưa phải một danh tính chắc chắn. Ghế phụ và dấu vết khác có thể đổi tập nghi vấn.')],
    setup=[op('Place',12,0),op('Place',10,1),op('Place',1,2)])

lesson('strategy-own-board',3,('When their own table turns against them','Khi bàn của họ quay lại ép họ'),
    ('Experience a board with only your faction cards and understand unavoidable friendly votes.','Thử bàn chỉ còn bài phe mình và hiểu vì sao phải bỏ phiếu vào bài mình.'),[
    read(('Too much presence can be a trap','Đông người chưa chắc là lợi thế'),'All four seated cards belong to your printed Village faction. There is no opposing card or Neutral target. Every eligible card still owes one ballot into another living card.',
        'Bốn lá đang ngồi đều thuộc nhóm Village của bạn. Không có bài đối phương hay Neutral để chọn. Mỗi lá đủ điều kiện vẫn phải bầu một lá sống khác.'),
    vote(10,11,1),vote(11,10,0),vote(12,10,0),vote(13,10,0),tally(),
    quiz('What pressure can leaving only opposing faction cards on the table create?','Để trên bàn chỉ còn bài nhóm đối phương có thể tạo sức ép gì?',('They must vote their own cards','Họ buộc bầu vào bài của họ'),('Their cards may skip every ballot','Bài của họ được bỏ qua phiếu'),
        'No self-vote does not mean no friendly vote. If all available targets belong to the same faction, that faction must vote within itself. Neutral targets or later skills can give alternatives, so inspect the actual board.',
        'Cấm tự bầu không có nghĩa cấm bầu cùng phe. Nếu mọi mục tiêu đều thuộc một nhóm, nhóm đó phải bầu lẫn nhau. Neutral hoặc kỹ năng về sau có thể mở lựa chọn khác, nên hãy nhìn bàn thật.')],
    setup=[op('Place',10,0),op('Place',11,1),op('Place',12,2),op('Place',13,3),phase(4)])

chapters=[
 dict(id='controls',title=copy('Hands on the cards','Làm chủ thao tác'),summary=copy('Read, place, retrieve, aim Hunter, vote, sacrifice and Purge.','Xem, đặt, rút bài, nhắm Hunter, bỏ phiếu, hiến tế và thanh trừng.')),
 dict(id='energy',title=copy('Spend your hearts wisely','Dùng tim đúng lúc'),summary=copy('Energy, zero-heart refusals, refills, movement and swaps.','Năng lượng, hết tim, hồi tim, dời bài và đổi chỗ.')),
 dict(id='cards',title=copy('One ability at a time','Mỗi lá một bí quyết'),summary=copy('Separate laboratories for each special card, with extra Decay and counter-bet lessons.','Bàn thử riêng từng lá đặc biệt, thêm bài Decay và đối đáp cược.')),
 dict(id='strategy',title=copy('Turn clues into a plan','Từ dấu vết thành nước đi'),summary=copy('Seven-seat notes, old infection paths and pressure on a faction-only board.','Sổ bảy ghế, đường lây từ lượt trước và thế ép một nhóm tự bầu nhau.'))]

def canonical_names(value):
    if isinstance(value,str):return value.replace('VillageChief','Reeve').replace('GameMaster','Game Master')
    if isinstance(value,list):return [canonical_names(v) for v in value]
    if isinstance(value,dict):return {k:canonical_names(v) for k,v in value.items()}
    return value
lessons=canonical_names(lessons)
from tutorial_curriculum_revision import revise
lessons=canonical_names(revise(globals()))
from tutorial_natural_turns import natural_turns
lessons=natural_turns(lessons,globals())
from tutorial_context_copy import contextual_copy
lessons=contextual_copy(lessons,globals())

if __name__=='__main__':
    target=ROOT/'Tools/Localization/tutorial_curriculum.json';target.write_text(json.dumps(dict(chapters=chapters,lessons=lessons),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Authored',len(lessons),'independent lessons with',sum(len(x['beats']) for x in lessons),'short beats.')

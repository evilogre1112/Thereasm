"""Setup tutorial heart workshop reference panels."""
from pathlib import Path
import json,re,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools'))
from migrate_scene_ui import Scene,SCRIPTS,guid,ref,sprite
HERE=Path(__file__).parent
# title, instruction, purpose, success; each pair is Vietnamese / English.
workshop=[
 [('Một nhịp tim, một quyết định','A heartbeat, a decision'),
  ('Nhìn bảng tim, rồi bấm ĐÃ HIỂU · TIẾP TỤC để tự thử.','Read the heart guide, then click GOT IT · CONTINUE to try it yourself.'),
  ('Khoan đi vội nhé. Tim là sức hành động, không phải máu của lá bài. Ta bắt đầu một vòng tập riêng với 5 tim, không thay đổi ván vừa học.','Stay a moment. Hearts are action energy, not a card’s health. We are starting a separate practice round with 5 hearts.'),
  ('Hãy thử tiêu tim và lấy lại chúng.','Let’s try spending hearts and recovering them.')],
 [('Đầu tư cho một chỗ ngồi','Invest in a seat'),
  ('Kéo CHUỘT TRÁI lá số 12 vào ghế viền xanh.','Left-drag card number 12 into the green seat.'),
  ('Đây là lá thuộc nhóm Villager, phe bạn đang điều khiển. Đặt nó tốn 2 tim: từ 5 còn 3. Vai trò bí mật không quyết định giá đặt bài.','This is a Villager-faction card, the side you control. Playing it costs 2 hearts: 5 becomes 3. Secret roles do not determine placement prices.'),
  ('Đúng 2 tim cho một lá phe mình; còn 3.','Two hearts for your faction’s card; 3 remain.')],
 [('Một bước ngắn hơn','A smaller step'),
  ('Kéo lá số 12 sang ghế trống viền xanh.','Drag number 12 to the empty green seat.'),
  ('Lá này được coi là đã hết khóa trong bài tập. Chuyển bài phe mình sang ghế trống chỉ tốn 1 tim, không phải giá đặt mới. Bài trung lập cũng chuyển ghế với giá 1 tim.','This practice card has finished its movement lock. Moving your card to an empty seat costs 1 heart, not a fresh placement. A neutral card also moves for 1.'),
  ('Chuyển ghế tốn 1 tim; bạn còn 2.','The move cost 1 heart; you have 2 left.')],
 [('Lùi một bước cũng có lợi','Stepping back can help'),
  ('Kéo lá số 12 về khay bài bên dưới rồi thả.','Drag number 12 back to the hand below and release.'),
  ('Rút bài phe mình hoặc trung lập hoàn tối đa 2 tim, không vượt phí đã đặt và không vượt trần 5. Lá này hoàn 2: từ 2 lên 4. Kỹ năng tự đẩy bài về không tặng khoản hoàn này.','Returning your or a neutral card refunds up to 2 hearts, capped by its placement payment and the 5-heart limit. This one refunds 2: 2 becomes 4. A forced skill return grants no refund.'),
  ('Bạn lấy lại 2 tim, nhưng không thể tích quá 5.','You recovered 2 hearts, but cannot stockpile beyond 5.')],
 [('Chạm vào phần sân bên kia','Reach across the table'),
  ('Bấm ĐÃ HIỂU · TIẾP TỤC để qua một vòng lượt mới.','Click GOT IT · CONTINUE to pass through a fresh turn cycle.'),
  ('Đừng nhầm màu áo với quyền thao tác. Bạn có thể đặt bài từ nhóm đối phương hoặc trung lập, nhưng thường phải trả cả 5 tim. Ta sẽ qua lượt để bạn thử với đủ tim.','You may play cards from the opposing or neutral faction, but normally pay all 5 hearts. Let’s pass the turns so you can try with a full budget.'),
  ('Lượt mới đã hồi đủ 5 tim.','Your new turn has restored all 5 hearts.')],
 [('Một lựa chọn đắt giá','An expensive choice'),
  ('Kéo lá số 3 vào ghế trống viền xanh.','Drag card number 3 into the empty green seat.'),
  ('Bạn vẫn đang điều khiển Villager; lá số 3 thuộc nhóm Cursermancer. Đặt nó tốn 5 tim, giống giá đặt một lá trung lập thường. Hãy nhìn thanh tim về 0.','You still control Villagers; number 3 belongs to the Cursermancer faction. Playing it costs 5 hearts, the same as a normal neutral card. Watch the meter drop to 0.'),
  ('Đặt bài nhóm khác đã dùng hết 5 tim.','Playing the other faction’s card used all 5 hearts.')],
 [('Hết tim chưa phải hết ván','No hearts is not game over'),
  ('Bấm ĐÃ HIỂU · TIẾP TỤC để qua lượt và nhận lại 5 tim.','Click GOT IT · CONTINUE to pass turns and refill to 5.'),
  ('Bạn không cần chờ đồng hồ hay mạng để hồi tim. Khi lượt hành động của mình bắt đầu, tim trở về 5; phần dư không cộng dồn. Nút này mô phỏng cả hai bên cùng kết thúc lượt.','Hearts refill to 5 at the start of your action turn; leftover hearts do not accumulate. This button passes both sides’ turns for practice.'),
  ('Lại đủ 5 tim. Thử chuyển bài phía bên kia nhé.','Back to 5. Now try moving the other faction’s card.')],
 [('Đổi vị trí của đối phương','Move the other faction’s card'),
  ('Kéo lá số 3 sang ghế trống viền xanh.','Drag number 3 to the empty green seat.'),
  ('Lá số 3 đã hết khóa. Chuyển bài đối phương trên bàn tốn 3 tim; bài phe mình hoặc trung lập chỉ tốn 1. Muốn ép bài đối phương về tay sẽ cần 5 tim.','Number 3 has finished its lock. Moving an opposing card costs 3 hearts; yours or a neutral card costs 1. Forcing an opposing card back to hand instead requires 5.'),
  ('Một lần chuyển bài đối phương đã tốn 3 tim; còn 2.','Moving the opposing card cost 3 hearts; 2 remain.')],
 [('Một chỗ ngồi, hai lựa chọn','One seat, two choices'),
  ('Bấm ĐÃ HIỂU · TIẾP TỤC để bắt đầu lượt tập tiếp theo.','Click GOT IT · CONTINUE to start the next practice turn.'),
  ('Thả bài lên một lá đang ngồi là thay hoặc đổi chỗ, không phải đặt vào ghế trống. Có bài đối phương trong lần thay từ tay này nên giá là 5 tim. Ta qua lượt trước nhé.','Dropping onto an occupied card replaces or swaps cards rather than filling an empty seat. This hand replacement involves an opposing card, so costs 5. Let’s pass turns first.'),
  ('Bạn có 5 tim cho lần thay bài.','You have 5 hearts for the replacement.')],
 [('Nhường chỗ cho lá mới','Make room for a new card'),
  ('Kéo lá số 12 từ tay lên đúng lá số 3 viền xanh.','Drag number 12 from hand onto the green-highlighted number 3.'),
  ('Lá số 3 sẽ về tay chủ sở hữu gốc, lá số 12 ngồi thay. Bạn trả 5 tim cho cả thao tác; lá bị thay không được hoàn tim riêng. Nếu cả hai là bài mình hoặc trung lập thì giá từ tay chỉ là 3.','Number 3 returns to its original owner’s hand and number 12 takes the seat. The whole action costs 5, with no extra refund for the displaced card. A hand replacement using only your/neutral cards costs 3.'),
  ('Bạn đã thay bài thành công bằng đúng 5 tim.','You completed the replacement for exactly 5 hearts.')],
 [('Mang bảng giá vào đêm tối','Take the prices into the night'),
  ('Đọc bảng phí thao tác, rồi bấm ĐÃ HIỂU · TIẾP TỤC.','Read the movement costs, then click GOT IT · CONTINUE.'),
  ('Giờ bạn đã cảm nhận được cái giá của từng nước đi. Giữ bảng này trong đầu: ghế trống, ghế có bài và rút về tay là ba quyết định khác nhau. Lá đang khóa vẫn phải chờ.','You have felt the price of each move. Remember: an empty seat, an occupied seat and a return to hand are different decisions. Locked cards must still wait.'),
  ('Một ngân sách nhỏ, rất nhiều cách xoay chuyển.','A small budget, plenty of ways to turn the game.')],
 [('Dành tim cho thời khắc đúng','Save hearts for the right moment'),
  ('Đọc ngoại lệ và kỹ năng, rồi bấm ĐÃ HIỂU · TIẾP TỤC.','Read the exceptions and skills, then click GOT IT · CONTINUE.'),
  ('Đừng tiêu hết tim chỉ vì còn ghế trống. Một phát Hunter hay phép Witch có thể cần trọn 5 tim. Luôn đọc mô tả trước, nhất là khi Totem bị Saboteur khóa kỹ năng.','Do not spend everything just because a seat is open. A Hunter shot or Witch cast can need all 5 hearts. Read first, especially when Saboteur silences Totem.'),
  ('Bạn đã sẵn sàng tự chọn nước đi đáng giá.','You are ready to choose which moves are worth their cost.')]
]
workshop.insert(11,[
  ('Đổi chỗ cho nhau, trả giá thế nào?','What does a swap cost?'),
  ('Đọc các tình huống đổi bài, rồi bấm ĐÃ HIỂU · TIẾP TỤC.','Read the swap examples, then click GOT IT · CONTINUE.'),
  ('Có lúc bạn không cần thêm người, chỉ cần đổi chỗ hai người đang ngồi. Nhìn xem có bao nhiêu lá thuộc phe đối phương trong cặp đó; giá đổi sẽ khác nhau.','Sometimes you do not need another card, just different seats for two that are already there. Count the opposing-faction cards in the pair: that changes the cost.'),
  ('Bạn đã biết khi nào đổi chỗ đáng để tiêu tim.','You know when a swap is worth spending hearts on.')])
# Never leave a bare price for a new player to interpret.
workshop=[[list(pair) for pair in row] for row in workshop]
for row in workshop:
    for pair in row:
        pair[0]=pair[0].replace('từ 5 còn 3','từ 5 tim còn 3 tim').replace('còn 3.','còn 3 tim.').replace('còn 2.','còn 2 tim.').replace('giá 1 tim','giá 1 tim')
        pair[0]=pair[0].replace('hoàn 2: từ 2 lên 4','hoàn 2 tim: từ 2 tim lên 4 tim').replace('từ 2 lên 4','từ 2 tim lên 4 tim')
        pair[0]=pair[0].replace('giá từ tay chỉ là 3.','thay từ tay chỉ tốn 3 tim.').replace('trần 5.','trần 5 tim.')
        pair[1]=pair[1].replace('5 becomes 3.','5 hearts becomes 3 hearts.').replace('3 remain.','3 hearts remain.').replace('2 remain.','2 hearts remain.').replace('2 left.','2 hearts left.')
overview=(
 'TIM LÀ SỨC HÀNH ĐỘNG\n\nMỗi phe có ngân sách riêng, tối đa 5 tim. Đây không phải máu của lá bài.\n\nĐặt bài, chuyển ghế, thay/đổi bài và một số kỹ năng sẽ tiêu tim. Giá dựa vào nhóm bài gốc, không dựa vào vai trò bí mật.\n\nMỗi khi bắt đầu lượt hành động của mình, bạn được hồi đủ 5 tim. Tim dư không cộng dồn. Đến pha Hiến tế, bạn dùng số tim còn lại; bỏ phiếu không tốn tim.\n\nĐừng chỉ đọc nhé: bài tập riêng tiếp theo cho bạn tự nhìn tim giảm và được hoàn lại.',
 'HEARTS ARE ACTION ENERGY\n\nEach side has its own budget, capped at 5 hearts. They are not card health.\n\nPlaying, moving, replacing/swapping cards and some skills spend hearts. Prices follow printed factions, not secret roles.\n\nEach new action turn refills your hearts to 5; leftovers do not accumulate. Sacrifice uses your remaining hearts, and voting itself costs no hearts.\n\nLet’s do more than read: in the separate practice round, watch hearts decrease and return as you act.')
movement=(
 'CHO LÁ BÀI MỘT CHỖ NGỒI\nĐưa bài thuộc phe bạn từ tay xuống ghế trống tốn 2 tim. Nếu lá thuộc phe đối phương hoặc trung lập, bạn cần trả 5 tim.\n\nĐƯA LÁ BÀI SANG GHẾ KHÁC\nKhi đã hết khóa, chuyển bài mình hoặc trung lập sang ghế trống tốn 1 tim. Chuyển bài đối phương tốn 3 tim.\n\nLẤY LÁ BÀI VỀ TAY\nTự rút bài mình hoặc trung lập được hoàn tối đa 2 tim, không vượt số tim đã trả khi đặt. Bạn không thể có hơn 5 tim.\nÉp bài đối phương về tay thì bạn phải trả 5 tim; không ai được hoàn tim.\n\nVừa rồi, bạn chuyển lá số 12 hết 1 tim rồi rút về để lấy lại 2 tim. Đôi khi lùi một bước là để chuẩn bị cho bước tiếp theo.',
 'GIVE A CARD A SEAT\nPlaying your faction’s card from hand into an empty seat costs 2 hearts. An opposing or neutral card costs 5 hearts.\n\nMOVE TO ANOTHER SEAT\nOnce unlocked, moving your or a neutral card to an empty seat costs 1 heart. Moving an opposing card costs 3 hearts.\n\nBRING A CARD HOME\nReturning your or a neutral card refunds up to 2 hearts, never more than you paid to place it. You cannot hold more than 5 hearts.\nForcing an opposing card back costs you 5 hearts. Nobody receives a refund.\n\nYou just spent 1 heart to move number 12, then recovered 2 hearts by returning it. Sometimes a step back prepares your next move.')
swaps=(
 'ĐỔI CHỖ HAI LÁ ĐANG TRÊN BÀN\nNếu cả hai là bài mình hoặc trung lập, bạn trả 2 tim cho cả lần đổi.\nNếu có đúng một lá thuộc phe đối phương, lần đổi tốn 3 tim.\nNếu cả hai lá đều thuộc phe đối phương, lần đổi tốn 5 tim.\n\nĐƯA BÀI TỪ TAY LÊN GHẾ ĐÃ CÓ NGƯỜI\nLá cũ về tay chủ sở hữu gốc, lá mới ngồi thay. Nếu cặp bài chỉ gồm bài mình hoặc trung lập, bạn trả 3 tim. Có bất kỳ lá đối phương nào trong cặp thì bạn trả 5 tim. Lá bị thay không được hoàn tim riêng.\n\nNhư lúc bạn dùng lá số 12 thay lá số 3: cả thao tác tốn 5 tim. Bạn không thể đổi ngược cùng một cặp liên tục trong lượt; lá đang khóa cũng chưa thể di chuyển.',
 'SWAP TWO CARDS ALREADY ON THE BOARD\nIf both are yours or neutral, pay 2 hearts for the whole swap.\nIf exactly one belongs to the opposing faction, the swap costs 3 hearts.\nIf both belong to the opposing faction, it costs 5 hearts.\n\nREPLACE A SEATED CARD FROM YOUR HAND\nThe old card returns to its original owner’s hand and the new card takes its seat. A pair containing only your/neutral cards costs 3 hearts. Any opposing card in the pair makes it cost 5 hearts. The displaced card gets no extra refund.\n\nWhen number 12 replaced number 3, the whole action cost 5 hearts. You cannot repeatedly reverse the same pair in one turn, and locked cards must wait.')
skills=(
 'DÀNH TIM CHO KỸ NĂNG\nHunter bắn hoặc Witch dùng phép đều cần 5 tim. Đừng tiêu hết vào di chuyển nếu bạn đang chờ ra đòn. Kỹ năng Gambler và Invoker hiện không tốn tim; vẫn phải đúng mục tiêu và thời điểm.\n\nMỘT VÀI LÁ CÓ CÁCH TRẢ GIÁ RIÊNG\nTotem còn kỹ năng: đặt hoặc chuyển ghế tốn 1 tim; tự rút được hoàn 1 tim. Ép Totem đối phương về tay cũng tốn 1 tim.\nĐưa Prankster từ tay lên bàn tốn 3 tim, dù lá đó thuộc nhóm nào.\n\nKHI KHÔNG CÒN TIM, BẠN VẪN CÓ THỂ…\nXem bài, đổi tab, ghi sổ, xem nghĩa trang, chọn vai trò, nguyền rủa, bỏ phiếu và kết thúc lượt đều không tốn tim. Thanh trừng cũng miễn phí, nhưng mỗi phe chỉ có 2 lần dùng mỗi ván. Thiếu tim thì thao tác bị từ chối, không trừ thêm.',
 'SAVE HEARTS FOR YOUR SKILLS\nA Hunter shot or Witch cast needs 5 hearts. Do not spend everything on movement if you are waiting to strike. Gambler and Invoker currently cost no hearts; their targets and timing must still be legal.\n\nSOME CARDS HAVE THEIR OWN PRICES\nAn unsilenced Totem costs 1 heart to play or move; returning it yourself refunds 1 heart. Forcing an opposing Totem back also costs 1 heart.\nPlaying Prankster from hand costs 3 hearts, regardless of its faction.\n\nWHEN YOUR HEARTS RUN OUT, YOU CAN STILL…\nInspect, switch tabs, use notes, visit the Cemetery, choose roles, infect, vote and end your turn without spending hearts. Purge is also free, but each side has only 2 uses per match. An unaffordable move is rejected without an extra deduction.')

story=json.loads((HERE/'tutorial_story.json').read_text(encoding='utf-8'))
if len(story['purposes'])==27:
    for field,index in [('purposes',2),('successes',3)]:story[field][26:26]=[list(row[index]) for row in workshop]
    if '26' in story['overrides']:story['overrides']['39']=story['overrides'].pop('26')
elif len(story['purposes'])==39:
    for field in ['purposes','successes']:story[field].insert(37,'')
    for old in [38,37]:
        if str(old) in story['overrides']:story['overrides'][str(old+1)]=story['overrides'].pop(str(old))
for i,row in enumerate(workshop,26):
    story['overrides'][str(i)]={field:list(row[j]) for j,field in enumerate(['title','instruction'])}
    story['purposes'][i]=list(row[2]);story['successes'][i]=list(row[3])
# A card name stays English even in a Vietnamese heading.
story['overrides'].setdefault('8',{})['title']=['Đưa Hunter lên bàn','Bring Hunter to the board']
story['overrides']['39']={'title':['Đến lượt bạn giữ ngôi làng','Your turn to guard the village'],
 'instruction':['Bạn đã thử cả hai phe và biết dùng tim. Hãy mang những điều vừa học vào một ván với bot!','You have tried both sides and learned to use hearts. Take what you learned into a match against a bot!']}
(HERE/'tutorial_story.json').write_text(json.dumps(story,ensure_ascii=False,indent=2),encoding='utf-8')

lesson=ROOT/'Assets/UI/Tutorial/FirstMatchLesson.asset';raw=lesson.read_text(encoding='utf-8')
head,rest=raw.split('  steps:\n',1);steptext,tail=rest.split('  cards:\n',1)
blocks=re.findall(r'(?ms)^  - stage: .*?(?=^  - stage: |\Z)',steptext)
if len(blocks)==27:blocks[26:26]=['']*13
elif len(blocks)==39:blocks.insert(37,'')
assert len(blocks)==40
for i in range(40):
    if 26<=i<39:values={field:workshop[i-26][j][0] for j,field in enumerate(['title','instruction','purpose','success'])}
    else:
        values={field:json.loads(re.search(r'(?m)^    '+field+r': (.*)$',blocks[i])[1]) for field in ['title','instruction','purpose','success']}
        values.update({k:v[0] for k,v in story['overrides'].get(str(i),{}).items()})
    blocks[i]='  - stage: '+str(i)+'\n'+''.join('    '+k+': '+json.dumps(v,ensure_ascii=False)+'\n' for k,v in values.items())
tail=re.sub(r'(?m)^  inspectDescription: .*$',lambda m:m[0].replace('DÂN LÀNG','Villager'),tail)
for field,pair in [('heartOverview',overview),('movementCosts',movement),('swapCosts',swaps),('skillCosts',skills)]:
    tail=re.sub(r'(?m)^  '+field+r': .*\n?','',tail)
    tail+='  '+field+': '+json.dumps(pair[0],ensure_ascii=False)+'\n'
lesson.write_text(head+'  steps:\n'+''.join(blocks)+'  cards:\n'+tail,encoding='utf-8')

SCRIPTS['TutorialController']=guid('Assets/Scripts/TutorialController.cs')
s=Scene('Assets/Scenes/TutorialBoard.unity',allow_migrated=True)
def named(name):return next((i for i in s.records if s.types[i]==1 and s.value(i,'m_Name').strip('"')==name),None)
panel=named('Heart Workshop Reference')
if panel is None:
    panel,tr=s.node('Heart Workshop Reference',7000000000000000013,(420,164,780,540),active=False)
    s.image(panel,sprite('painted-panel'),sliced=True,ppu=16,raycast=False)
    text,rt=s.label('Heart Workshop Text',tr,(30,26,720,488),overview[0],size=21,tint=(.94,.87,.72,1))
else:
    text=s.component(named('Heart Workshop Text'),script=SCRIPTS['Text'])
SCRIPTS['DialogueModalGuard']=guid('Assets/Scripts/DialogueModalGuard.cs')
if s.component(panel,script=SCRIPTS['DialogueModalGuard']) is None:s.mono(panel,'DialogueModalGuard')
panel_rect=s.component(panel,type_=224)
if named('Heart Workshop Paper') is None:
    paper,paper_rect=s.node('Heart Workshop Paper',panel_rect,(10,10,760,520))
    s.image(paper,tint=(.10,.018,.012,1),raycast=False)
    s.set_children(panel_rect,[paper_rect]+[c for c in s.children(panel_rect) if c!=paper_rect])
controller=s.find_script(SCRIPTS['TutorialController'])
s.set(controller,'heartGuidePanel',ref(panel));s.set(controller,'heartGuideText',ref(text))
# Separate top/bottom caption lanes prevent overlap.
for frame,caption in [(7000000000000000330,7000000000000000334),(7000000000000000338,7000000000000000342)]:
    for tr in s.children(frame):
        if tr!=caption and not s.value(s.game_object(tr),'m_Name').strip('"').startswith('Tutorial Callout Background'):continue
        s.set(tr,'m_Pivot','{x: 0.5, y: 0.5}');s.set(tr,'m_SizeDelta','{x: 204, y: 40}')
s.save()

# Translate display text without changing canonical network strings.
replacements={'Người mù':'Blind','Người điều khiển':'Controller','Thám tử':'Detective','Trưởng lão':'Elder','Con bạc':'Gambler',
 'Người gọi hồn':'Invoker','Quản trò':'Game Master','Quản Trò':'Game Master','Kẻ chơi khăm':'Prankster','Giáo sĩ':'Preacher',
 'Trưởng làng':'Reeve','Kẻ phá hoại':'Saboteur','Vật hiến tế':'Sacrifier','Kẻ dò xét':'Snooper','Vật tổ':'Totem','Thương nhân':'Trader','Phù thủy':'Witch','Thợ săn':'Hunter'}
def normalize(entries):
    for e in entries:
        if 'card.name.' in e['key']:e['vietnamese']=e['english']
        if any(word in e['key'].lower() for word in ['description','suspicion']) or '<b>DẤU NGHI NGỜ' in e['vietnamese']:
            for old,new in replacements.items():e['vietnamese']=e['vietnamese'].replace(old,new)
    return entries
catalog=ROOT/'Assets/UI/Localization/GameLocalization.asset';raw=catalog.read_text(encoding='utf-8');header,rawentries=raw.split('  entries:\n',1)
entries=[]
for block in re.split(r'(?m)^  - key: ',rawentries)[1:]:
    e={'key':json.loads(block.split('\n',1)[0])}
    for field in ['source','english','vietnamese']:e[field]=json.loads(re.search(r'(?m)^    '+field+': (.*)$',block)[1])
    entries.append(e)
def add(key,pair):
    e={'key':key,'source':pair[0],'english':pair[1],'vietnamese':pair[0]}
    for i,existing in enumerate(entries):
        if existing['source']==pair[0] or existing['key']==key:entries[i]=e;break
    else:entries.append(e)
for i,row in enumerate(workshop,26):
    for j,field in enumerate(['title','instruction','purpose','success']):add('guided.'+str(i)+'.'+field,row[j])
add('guided.8.title',story['overrides']['8']['title'])
for field,pair in [('heartOverview',overview),('movementCosts',movement),('swapCosts',swaps),('skillCosts',skills)]:add('guided.'+field,pair)
for field,pair in story['overrides']['39'].items():add('guided.39.'+field,pair)
for field,values in [('purpose',story['purposes']),('success',story['successes'])]:add('guided.39.'+field,values[39])
inspection=json.loads(re.search(r'(?m)^  inspectDescription: (.*)$',tail)[1])
old=next(e for e in entries if 'Đây là một lá thường' in e['vietnamese'])
add('guided.inspection', [inspection,old['english'].replace('VILLAGERS','Villager')])
entries=normalize(entries)
catalog.write_text(header+'  entries:\n'+''.join('  - key: '+json.dumps(e['key'],ensure_ascii=False)+'\n'+''.join('    '+k+': '+json.dumps(e[k],ensure_ascii=False)+'\n' for k in ['source','english','vietnamese']) for e in entries),encoding='utf-8')
(HERE/'catalog.json').write_text(json.dumps({'entries':entries},ensure_ascii=False,indent=2),encoding='utf-8')
print('Saved 40 tutorial steps with full sentences and explicit heart units, separated captions and English card names.')
if (HERE/'voice_tutorial.py').exists():
    import runpy
    runpy.run_path(str(HERE/'voice_tutorial.py'))

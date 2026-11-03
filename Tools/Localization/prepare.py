"""Compile bilingual localization strings into catalog asset format."""
from pathlib import Path
import json,re,uuid
ROOT=Path(__file__).resolve().parents[2]
rows=json.loads((Path(__file__).parent/'sources.json').read_text(encoding='utf-8-sig'))['rows']
sources={r['key']:r['source'] for r in rows}
entries={}
def pair(source,en,vi,key=None):
    if not source:return
    en=re.sub(r'\bCurs?\b','Cursermancer',en)
    entries[source]=dict(key=key or 'text.'+str(len(entries)),source=source,english=en,vietnamese=vi)
def vi(source,en):pair(source,en,source)
def key(k,translated,english_source=False):
    source=sources[k];pair(source,source if english_source else translated,translated if english_source else source,k)

names={'Blind':('Blind','Người mù'),'Controller':('Controller','Người điều khiển'),'Detactive':('Detective','Thám tử'),
 'Elder':('Elder','Trưởng lão'),'Gambler':('Gambler','Con bạc'),'Invoker':('Invoker','Người gọi hồn'),'Jester':('Jester','Hề'),
 'GameMaster':('Game Master','Quản trò'),'Prankster24':('Prankster','Kẻ chơi khăm'),'Preacher':('Preacher','Giáo sĩ'),
 'Reeve':('Reeve','Trưởng làng'),'Saboteur':('Saboteur','Kẻ phá hoại'),'Sacrifier':('Sacrifier','Vật hiến tế'),
 'Snooper22':('Snooper','Kẻ dò xét'),'Totem':('Totem','Vật tổ'),'Trader':('Trader','Thương nhân'),'Villager':('Villager','Dân làng'),
 'Wỉtch':('Witch','Phù thủy'),'Hunter':('Hunter','Thợ săn')}
for source,(en,vn) in names.items():pair(source,en,en,'card.name.'+en)
descriptions={
'Blind19':'[Bị động] Phải ngồi ở ghế sát Nghĩa trang. Công bố danh tính và trạng thái nhiễm bệnh của lá chết mới nhất.\n[Phân rã] [Bị động] Khi bị nhiễm, Blind công bố thông tin sai về lá đó. Cursermancer vẫn luôn thấy sự thật.',
'Controller22':'[Bị động] Đảo ngược luật Hiến tế. Khi lá này ở trên bàn, các lá có số phiếu thấp nhất nhưng ít nhất 1 phiếu sẽ bị hiến tế, thay cho những lá nhiều phiếu nhất.',
'Detactive23':'[Bị động] [Một lần mỗi lần đặt] Nếu ngồi giữa hai lá, xác định lá đáng nghi hơn và cộng 1 điểm Nghi ngờ cho lá đó; ưu tiên Cursermancer.\nNghi ngờ cơ bản: Cursermancer = 2, Dân làng = 1.',
'Elder17':'[Bị động] [Hào quang] Các lá trong vùng ảnh hưởng không thể bị di chuyển.',
'Gambler24':'[Chủ động] Bí mật cược vào một mục tiêu. Thắng nếu bạn là người bỏ phiếu thứ nhất và chỉ mình bạn cược, hoặc bạn là người bỏ phiếu thứ hai và cả hai đều cược.\nThắng: mục tiêu được cộng 1 phiếu.\nThua: mục tiêu được phe bạn bỏ nhiều phiếu nhất bị trừ 1 phiếu.',
'Invoker19':'[Chủ động] Trong pha Hiến tế, người đặt lá này gần nhất có thể dùng kỹ năng khi nó ngồi sát Nghĩa trang. Bấm chuột trái để xem tối đa 3 lá đã chết và chọn 1 lá hồi sinh về tay chủ sở hữu gốc. Mỗi nghi thức dùng một lần; hoàn tất lựa chọn mới tính là đã dùng.',
'Jester20':'[Bị động] Gây hỗn loạn khi Hiến tế! Có cơ hội bí mật tạo thêm 1 phiếu (30%), 2 phiếu (25%) hoặc 3 phiếu (20%) vào các mục tiêu ngẫu nhiên khác bản thân.',
'Master21':'[Bị động] Tạo ghế phụ phía trên và dưới; thêm ghế bên trái khi ở ghế số 1. Ghế phụ có bài chỉ khóa thao tác di chuyển thủ công. Kỹ năng đẩy sẽ mang theo các ghế và bài trên đó. Nếu bị trả về tay, toàn bộ bài trên ghế phụ cũng về tay chủ sở hữu tương ứng. Mất ghế trái sẽ trả lá trên ghế đó về tay. Khi Quản trò chết, tất cả bài trên ghế phụ chết theo, kể cả có khiên Giáo sĩ.',
'Prankster24':'[Bị động] Đặt bài tốn 3 tim. Khi có bài ở ghế liền bên trái, đẩy lá đó sang một ghế chính đang trống ngẫu nhiên. Riêng lúc vừa đặt: có 10% cơ hội trả lá đó về tay. Trưởng lão chặn hiệu ứng này. Nếu Quản trò bị đẩy, các ghế phụ và bài trên đó đi theo; nếu về tay thì chúng cũng về tay tương ứng.',
'Preacher18':'[Bị động] [Hào quang] Các mục tiêu được bảo vệ khỏi lây nhiễm của Cursermancer và phát bắn của Thợ săn.',
'Reeve17':'[Bị động] Khi còn ít nhất một Dân làng thường trên bàn, lập tức kết thúc pha đặt bài và chuyển sang pha Nguyền rủa.',
'Saboteur18':'[Bị động] [Hào quang] Khóa kỹ năng gốc của hai lá kề ngang. Không khóa khả năng lây nhiễm của Cursermancer.',
'Sacrifier21':'[Bị động] Nếu ở trên bàn trong pha Hiến tế, lá này ghi đè kết quả bỏ phiếu và bị hiến tế thay cho các mục tiêu được bầu.',
'Snooper22':'[Hiến tế] [Kết hợp]\n[Bị động] Tự động thêm 1 phiếu vào Cursermancer khi Hiến tế.\n[Chủ động] [Phân rã] Khi bị nhiễm, phe Cursermancer điều khiển và tự chọn mục tiêu cho phiếu này.',
'Totem1':'[Bị động] Chỉ tốn 1 tim để đặt lá này lên bàn hoặc thu về tay.',
'Totem2':'[Bị động] Chỉ tốn 1 tim để đặt lá này lên bàn hoặc thu về tay.',
'Trader20':'Sau khi giải quyết mọi phiếu cộng và phiếu trừ, lấy số phiếu cao nhất trong nhóm gồm lá này và các lá kề ngang, rồi chia đều chỉ cho những lá đang có. Làm tròn xuống và bỏ phần dư.',
'Villager1':'Không có kỹ năng đặc biệt. Dựa vào suy luận và bỏ phiếu trong pha Hiến tế để tìm ra kẻ mang lời nguyền.',
'Villager9':'[Chủ động] Giữ chuột phải rồi kéo mũi ngắm tới một lá địch để tiêu diệt ngay. Chỉ dùng trước khi pha Nguyền rủa bắt đầu. Sau khi bắn, kỹ năng chỉ hồi lại khi pha Hiến tế/Xử tử đã kết thúc.',
'Witch23':'[Chủ động] [Một lần mỗi nghi thức] Tốn 5 tim: bấm chuột trái để tiêu diệt mọi lá bị nhiễm khác trên bàn. Không ảnh hưởng bản thân hoặc bài trên tay. Hồi lại sau khi pha Hiến tế/Xử tử kết thúc.'}
generic={
 'curDescription':'[Chủ động] Lây nhiễm cho lá chưa nhiễm gần nhất theo một trong bốn hướng: trên, dưới, trái, phải.',
 'infectedDescription':'[Trạng thái] Lá này đã bị nhiễm bệnh. Nếu bị Thanh trừng phát hiện, lá sẽ bị tiêu diệt.',
 'curInfectedDescription':'[Chủ động] Có thể lây nhiễm cho đối phương. Bản thân đang bị nhiễm bệnh và có thể bị tiêu diệt bởi Thanh trừng.'}
for r in rows:
    k=r['key'];source=r['source']
    if not any(k.endswith('.'+f) for f in ['description','curDescription','infectedDescription','curInfectedDescription']) or not source:continue
    asset,field=k.split('.',1)
    if asset.startswith('Villager') and asset!='Villager9':base=descriptions['Villager1']
    else:base=descriptions.get(asset,descriptions['Villager1'])
    if source.startswith('Effect: [Active] Infects'):vn=generic['curInfectedDescription' if 'shadows' in source else 'curDescription']
    elif source.startswith('Effect: [Status]'):vn=generic['infectedDescription']
    else:vn=base+(' [<color=red>Đã nhiễm</color>]' if '<color=red>Infected</color>' in source else '')
    # Card names remain English inside otherwise Vietnamese descriptions.
    for en,translated_name in names.values():
        if translated_name != 'Dân làng':vn=vn.replace(translated_name,en)
    pair(source,source,vn,k)

quotes=[
'Mắt nhìn dễ khiến ta lạc lối. Ta thấy sự thật.','Ta thấy tất cả.','Bóng tối đang bám lấy ngươi. Hãy bước ra ánh sáng.',
'Cursermancer đang ẩn giữa chúng ta. Đó là điều hợp lý nhất.','Ta đang tính lại khả năng. Có kẻ đang nói dối.','Lá phiếu là tấm khiên cuối cùng trước bóng tối.',
'Để xem ai đang nấp sau những chiếc mặt nạ.','Mỗi chuyện có hai phía, nhưng sự thật chỉ có một.','Ta đang để mắt tới cả hai người.',
'Ta từng thấy lời nguyền này... từ nhiều kiếp trước.','Ta sẽ giữ vững chỗ này cho tới bình minh, hoặc ngày tận thế.','Đây có phải dấu chấm hết không?',
'Cược lớn, thắng lớn. Đặt cược đi nào!','Ta thấy ngươi đang bịp. Ta cược thêm một mạng.','Ta tính cả rồi. Ngươi sẽ thua.','Hai con một... có vẻ vận may của ai đó vừa cạn.',
'Nghĩa trang không phải kết thúc. Chỉ là phòng chờ thôi.','Ta nghe người chết thì thầm... họ muốn quay về.','Một linh hồn đổi một linh hồn. Cán cân phải được giữ.',
'Úm ba la... người tiếp theo là ai đây?','Hê hê... đừng nhìn ta, ta chỉ là thằng hề.','Thêm phiếu! Thêm hỗn loạn! THÊM NỮA!',
'Đừng nhìn ta. Hãy nhìn kẻ nói dối ngồi cạnh ngươi.','Nước đi thú vị... hay sai lầm chết người? Rồi sẽ rõ.','Tích tắc... nghi thức chẳng đợi ai.',
'Chỗ này rõ ràng dành cho ta.','Trông ngươi căng thẳng quá. Muốn ta giúp đổi chỗ không?','Đừng nhìn ta, ta đang ngoan mà... tạm thời thôi.','Yên ắng quá. Phải trêu ai đó mới được.',
'Lùi lại, Cursermancer! Bóng tối của ngươi không chạm được chúng ta.','Đừng sợ bóng tối, ánh sáng đang ở bên ta.','Hãy ở trong vòng tròn. Đức tin của ta mạnh hơn nghi thức của ngươi.',
'Định nói suốt ngày hay định sống sót đây?','Ta là luật ở đây, và ta nói chờ thế đủ rồi.','Giữ trật tự trong làng!',
'Ồ? Sao hai người hàng xóm đáng mến hôm nay im lặng thế?','Đừng phí sức. Ta cắt dây của ngươi rồi.','Lỗi hệ thống ư? Không, là ta đấy.',
'Sao lại là ta?! Còn người khác mà... xin hãy chọn ai khác!','Toàn lũ quái vật! Các ngươi giết ta để cứu mình!','Cứu với! Họ đang ép ta vào vòng tròn! Ai cứu ta với!','Ta không chịu! Ta không để bóng tối mang mình đi đâu!',
'Mọi thứ đều có giá. Kể cả mạng sống của ngươi.','Trao đổi công bằng ư? Làng này làm gì có chuyện đó.','Những lá phiếu đang đổi chỗ... rồi sẽ rơi vào đâu?','Ta buôn bí mật và sự an toàn. Muốn giao kèo chứ?',
'Tôi chỉ là người dân bình thường, tôi thề!','Đừng nhìn tôi như thế... tôi sợ.','Bạn có nghe tiếng động ở ghế bên cạnh tôi không?','Lạnh quá... có ai đang nhìn chúng ta sao?',
'Một phát, một mạng. Ta không bao giờ bắn trượt.','Hừm... mục tiêu của ta ở đâu?','Cursermancer đang ở gần. Ta ngửi thấy mùi bệnh trong không khí.',
'Ghế ngồi định tầm nhìn. Chẵn hay lẻ, bóng tối chẳng thiên vị ai.','Đừng nhìn chằm chằm. Ngươi đã tự bỏ phép thuật, đừng thèm muốn phép của ta.','Bệnh dịch thì thầm, ẩn trong những con số. Ta nghe thấy nó.'
]
unique=[]
for r in rows:
    if '.quote.' in r['key'] and r['source'] not in [x['source'] for x in unique]:unique.append(r)
assert len(unique)==len(quotes),(len(unique),len(quotes))
for r,vn in zip(unique,quotes):pair(r['source'],r['source'],vn,r['key'])

tutorial=[
('Meet the board','Click Start tutorial. You will control both sides on a sample board.','Gold outline: start here. Green outline: drop here. This lesson uses 7 fixed cards and skips the neutral draft.','Start by choosing the secret roles.'),
('Choose the first Cursermancer','LEFT-CLICK card NUMBER 1 with the gold outline in the hand.','Cursermancer is a secret role assigned to a card. A two-player match needs one in each original faction.','Card 1 is now a secret Cursermancer.'),
('Open the Villager hand','Click the VILLAGERS tab at the top of the hand below.','Tabs change the original faction you are viewing. You may play another faction\'s cards, but it costs more hearts.','You are viewing the Villager hand.'),
('A disguised Cursermancer','LEFT-CLICK card NUMBER 11 with the gold outline.','A Villager card can secretly be a Cursermancer. Hunter number 9 cannot be selected.','Card 11 is a Cursermancer disguised among the Villagers.'),
('Start the match','Both Cursermancers are selected. Click the highlighted START MATCH button.','Villagers act first in this lesson. Each action turn grants 5 hearts to play cards or use skills.','The Villager turn starts with 5 hearts.'),
('Inspect a card','RIGHT-CLICK card NUMBER 10 in the hand.','Right-click enlarges a card and shows its description. Inspection is free and does not activate a skill.','The card and its description are enlarged.'),
('Close the inspection','RIGHT-CLICK the enlarged card again.','Close inspection before dragging. The card returns to its original hand and size.','You can now inspect and close a card.'),
('Play your first card','HOLD LEFT MOUSE on card NUMBER 10, drag it to the GREEN OUTLINED SEAT, then release.','Playing a card of the active faction costs 2 hearts. A seat holds one card on the board; it is not a hand.','Card placed: 5 hearts → 3 hearts.'),
('Play the Hunter','LEFT-DRAG card NUMBER 9 to the green outlined seat, then release.','Hunter can use its skill only on the board. This second card costs another 2 hearts, leaving 1.','Hunter is on the board. Villagers have 1/5 hearts.'),
('Change turns','Click the highlighted END TURN button.','You do not have to spend every heart. The other side starts its turn with 5 hearts.','Cursermancer turn; the displayed hand has changed.'),
('See secret roles','Observe card NUMBER 1 showing its Cursermancer face, then click UNDERSTOOD · CONTINUE.','Cursermancer automatically sees true identities and infection, even when Blind reports a lie. Villagers see public information only. There is no reveal button.','Each side has its own view. Cursermancer does not need to hold a reveal button.'),
('Play the Cursermancer','LEFT-DRAG card NUMBER 1 to the green outlined seat.','Cursermancer remains disguised to the other side. Playing a Cursermancer-faction card on its own turn also costs 2 hearts.','Card 1 is seated on the board in disguise.'),
('Back to Villagers','Click END TURN to switch to Villagers.','Villagers regain 5 hearts at the start of this turn: enough to try Hunter\'s skill.','Villagers have 5 hearts to use a skill.'),
('Use Hunter\'s skill','HOLD RIGHT MOUSE on card NUMBER 9, drag the aiming arrow to card NUMBER 1, then release.','RIGHT-DRAG aims a skill; RIGHT-CLICK inspects. The shot costs 5 hearts and sends the target to the Cemetery.','The correct target was shot, spending all 5 hearts.'),
('Open the Cemetery','Click the highlighted CEMETERY button on the left.','Destroyed cards are kept here. Viewing the Cemetery is free. Some skills interact with dead cards.','Card 1 is now in the Cemetery.'),
('Close the Cemetery','Click CLOSE on the Cemetery panel to return to the board.','Reopen it whenever you need to inspect eliminated cards. Dead cards cannot be played or vote.','You are back at the board.'),
('Try the ritual','Click TRY THE RITUAL. The lesson will place cards 11 and 2 for practice.','In a real match, a full board or Reeve starts the ritual. This is a prepared example and spends no hearts.','The example is ready. You are practicing the Cursermancer side.'),
('Infect a target','HOLD LEFT MOUSE on card NUMBER 11, drag to green outlined card NUMBER 9, then release.','Card 11 is the chosen Cursermancer. Infection travels along a row or column; empty seats block it. Infection is different from the Cursermancer role.','Hunter number 9 is infected. Card 11 remains the Cursermancer.'),
('Start the Sacrifice','Click the highlighted TO SACRIFICE button.','Both sides vote in turn. Each on-board card of the active original faction must choose a different card.','The Villager faction votes first.'),
('Card 10\'s vote','LEFT-DRAG from card NUMBER 10 to green outlined card NUMBER 2, then release.','The arrow now casts a vote; it does not move the card. A card cannot vote for itself.','Card 10 has voted for card 2.'),
('Hunter\'s vote','LEFT-DRAG from card NUMBER 9 to card NUMBER 2.','Each card casts its own vote. Using a skill or being infected does not exempt a living on-board card.','Card 9 has voted for card 2.'),
('The faction\'s last vote','LEFT-DRAG from card NUMBER 11 to card NUMBER 2.','Card 11 is secretly a Cursermancer but belongs to the Villager faction, so it votes with this group.','All 3 Villager-faction cards have voted.'),
('Confirm votes','Click CONFIRM VOTES to pass to the other side.','You can confirm only when every required card has voted. The opponent\'s votes remain private before the tally.','Villager votes confirmed. The Cursermancer faction votes next.'),
('The opponent\'s turn','Click VIEW OPPONENT TURN. The lesson will cast one sample vote from card 2.','Card 2 votes for card 10. When both sides confirm, the game tallies votes and applies skills.','Both sides have confirmed. Watch the tally.'),
('Watch the tally','Wait for the game to count votes, sacrifice card 2 and return surviving cards to hand.','The most-voted card is sacrificed. A tie for the highest count sacrifices all tied cards.','Card 2 received 3 votes and was sacrificed. Survivors returned to hand.'),
('Try Purge','The lesson switches to Villagers. Click the highlighted PURGE button.','Purge destroys every infected card, including cards in hidden hands. Each side has only 2 uses per match. It does not automatically find Cursermancers.','Card 9 was eliminated because it was infected. Villagers have 1/2 Purges left.'),
('You know how to play','You have chosen roles, played cards, used skills, infected and voted. Try a match against the bot!','Villagers win when no Cursermancer survives. Cursermancer wins when living Cursermancers ≥ living non-Cursermancer Villagers. This lesson is complete; no winner is declared.','Tutorial complete.')]
for i,step in enumerate(tutorial):
    for field,text in zip(('title','instruction','purpose','success'),step):key(f'tutorial.{i}.{field}',text)
key('tutorial.inspect','VILLAGER · NUMBER 10\n\nAn ordinary card with no active skill.\n\nPlay from the active faction: 2 hearts. Opponent or neutral cards: 5 hearts.\n\nRight-click again to close.')

ui={
'Chọn phe, tạo phòng hoặc nhập mã phòng của bạn.':'Choose a side, create a room or enter a room code.',
'Học cách chơi · hướng dẫn từng bước':'Learn to play · guided tutorial','Chơi Dân làng':'Play as Villagers','ĐẤU VỚI MÁY DÂN LÀNG':'AGAINST A VILLAGER BOT',
'Chọn phe Cursermancer':'Choose Cursermancer','Một ngôi làng. Hai phía của lời nguyền.\nChọn phe của bạn để bắt đầu.':'One village. Two sides of a curse.\nChoose your side to begin.',
'Đấu với bot Cursermancer. Quan sát nghi thức,\nsuy luận và dập tắt nguồn lây.':'Face a Cursermancer bot.\nObserve, deduce and end\nthe source of infection.',
'Tạo hoặc vào phòng bằng mã.\nChọn phe Cursermancer hoặc Dân làng\nđể đấu cùng bạn bè.':'Create or join with a code.\nChoose your side and play\nwith a friend.',
'Mã phòng · để trống khi tạo để lấy mã tự động':'Room code · leave blank to create a random code','Đã hiểu · Tiếp tục':'Understood · Continue','Thoát':'Quit',
'Cấp 1 phù hợp để làm quen':'Level 1 is a good place to start','CHƠI ONLINE · 2 NGƯỜI':'ONLINE · 2 PLAYERS','VD: LANG01':'E.g. VILLAGE01',
'ĐẤU VỚI MÁY CURSERMANCER':'AGAINST A CURSERMANCER BOT','CHIẾN THUẬT  /  SUY LUẬN  /  LỜI NGUYỀN':'STRATEGY  /  DEDUCTION  /  THE CURSE',
'Chọn phe Dân làng':'Choose Villagers','Tạo phòng':'Create room','Rời phòng':'Leave room','Chơi online':'Play online',
'CHUỘT TRÁI  Chọn / kéo bài     ·     CHUỘT PHẢI  Xem mô tả     ·     ESC  Luật chơi':'LEFT MOUSE  Select / drag     ·     RIGHT MOUSE  Inspect     ·     ESC  Rules',
'Luật chơi':'Rules','Phe Cursermancer':'Cursermancer','Phe Dân làng':'Villagers',
'Đấu với bot Dân làng. Chọn Cursermancer\nbí mật, lây nhiễm và bảo vệ\nlời nguyền.':'Face a Villager bot.\nChoose secret Cursermancers,\nspread and protect the curse.',
'Chơi Cursermancer':'Play as Cursermancer','ĐỘ KHÓ KHI ĐẤU BOT':'BOT DIFFICULTY','Vào phòng':'Join room',
'Cuộn để xem đầy đủ · Trận đấu đang tạm dừng':'Scroll to read · Match paused','Cuộn để xem đầy đủ · Trận online vẫn tiếp tục':'Scroll to read · Online match continues',
'Phe của bạn: CURSERMANCER\nChọn phe đang có người giữ sẽ đổi vai của cả hai.':'Your side: CURSERMANCER\nChoosing an occupied side swaps both players.',
'HAI NGƯỜI · HAI THIẾT BỊ':'TWO PLAYERS · TWO DEVICES','LUẬT CHƠI NHANH':'QUICK RULES','Đóng':'Close','Giữ để xem vai trò':'Hold to reveal roles',
'VÁN CHƠI KẾT THÚC':'MATCH OVER','VÁN CHƠI ĐÃ KẾT THÚC':'MATCH OVER','Chuột phải: xem bài\nESC: mở / đóng luật chơi':'Right-click: inspect\nESC: open / close rules',
'Chọn bài trung lập, sau đó chọn Cursermancer bí mật trước khi bắt đầu.':'Draft neutral cards, then choose secret Cursermancers before starting.',
'ĐIỀU KHIỂN':'CONTROLS','Kết thúc lượt':'End turn','NGHĨA TRANG · Chuột phải để xem bài':'CEMETERY · Right-click to inspect',
'Chưa có lá bài nào trong nghĩa trang.':'The Cemetery is empty.','VAI TRÒ BÍ MẬT':'SECRET ROLES','Ẩn / mở tay bài':'Hide / show hand',
'Dân làng':'Villagers','Chọn lá bên phải':'Choose right card','Trung lập':'Neutral','Nghĩa trang':'Cemetery','Chọn lá bên trái':'Choose left card',
'Chơi tiếp':'Play again','Chọn lá ở tay bài bên phải.\nThợ săn số 9 không thể là Cursermancer.':'Choose cards from the hand on the right.\nHunter number 9 cannot be a Cursermancer.',
'Đổi phe ván sau':'Swap sides next match','Về menu':'Back to menu','Sổ tay':'Notes','CHUẨN BỊ VÁN CHƠI':'MATCH SETUP','BẮT ĐẦU Ở ĐÂY':'START HERE',
'Bắt đầu học':'Start tutorial','Xem luật đầy đủ':'Read full rules','Mỗi lần chỉ thực hiện thao tác được chỉ sáng.':'Perform only the highlighted action at each step.',
'Chơi thử với bot':'Practice with a bot','THẢ VÀO ĐÂY':'DROP HERE','Học lại':'Restart tutorial','CỦA BẠN':'YOURS','ĐỐI THỦ':'OPPONENT',
'TRUNG LẬP':'NEUTRAL','TRUNG LẬP · BẠN':'NEUTRAL · YOU','TRUNG LẬP · ĐỊCH':'NEUTRAL · RIVAL',
'Xanh: bài của bạn · Cam: bài đối thủ · Tím: trung lập. Nhãn theo nhóm bài gốc, không tiết lộ vai trò bí mật.':'Cyan: yours · Amber: opponent · Purple: neutral. Labels show original factions, never secret roles.',
'PHE CURSERMANCER':'CURSERMANCER FACTION','PHE DÂN LÀNG':'VILLAGER FACTION','DÂN LÀNG':'VILLAGERS','CURSERMANCER':'CURSERMANCER',
'BÀI TRUNG LẬP':'NEUTRAL CARDS','BÀI PHE CURSERMANCER':'CURSERMANCER FACTION CARDS','BÀI PHE DÂN LÀNG':'VILLAGER FACTION CARDS',
'ÚP BÀI':'FACE DOWN',
'Thử nghi thức':'Try the ritual','Xem lượt đối phương':'View opponent turn','ĐÚNG RỒI!  ':'WELL DONE!  ',
'Bắt đầu ván':'Start match','Bắt đầu':'Start','Phe của bạn: ':'Your side: ',
'Chọn phe đang có người giữ sẽ đổi vai của cả hai.':'Choosing an occupied side swaps both players.',
'Vai trò đã được công khai. Chọn Chơi tiếp khi bạn sẵn sàng.':'Roles are now public. Choose Play again when ready.','Ván chơi kết thúc':'Match over',
'01  /  CHỌN BÀI TRUNG LẬP':'01  /  NEUTRAL DRAFT','02  /  BOT ĐANG CHỌN CURSERMANCER BÍ MẬT':'02  /  BOT SELECTING SECRET CURSERMANCERS',
'02  /  CHUẨN BỊ VAI TRÒ':'02  /  ROLE SETUP','Bạn điều khiển Dân làng. Vai trò Cursermancer được giữ kín trong trận.':'You control the Villagers. Cursermancer identities remain secret.',
'Đã đủ Cursermancer. Bấm Bắt đầu ván để vào lượt hành động.':'All Cursermancers selected. Click Start match to begin the action phase.',
'Chọn đủ Cursermancer ở từng nhóm bài, theo danh sách bên trái.':'Select the required Cursermancers in each faction using the checklist on the left.',
'ĐANG TỔNG KẾT PHIẾU':'TALLYING VOTES','Áp dụng kỹ năng, hiến tế và chuẩn bị vòng tiếp theo…':'Resolving skills, sacrifices and the next round…',
'NGHI THỨC NGUYỀN RỦA':'CURSE RITUAL','Kéo từ Cursermancer trên bàn tới mục tiêu cùng hàng hoặc cột. Ghế trống chặn lây nhiễm.':'Drag from an on-board Cursermancer to a target in its row or column. Empty seats block infection.',
'LƯỢT PHE CURSERMANCER':'CURSERMANCER TURN','LƯỢT PHE DÂN LÀNG':'VILLAGER TURN',
'Đặt bài mình: 2 tim · Bài đối phương / trung lập: 5 tim · Kỹ năng có chi phí riêng.':'Play your card: 2 hearts · Opponent / neutral: 5 hearts · Skills have their own costs.',
'Bot đang hành động… Mở Luật chơi để tạm dừng.':'The bot is acting… Open Rules to pause.','Thực hiện theo hướng dẫn phía trên.':'Follow the instructions above.',
'Đang tổng kết…':'Tallying…','Chốt phiếu':'Confirm votes','Sang hiến tế':'To Sacrifice','Đang chờ đối phương…':'Waiting for the opponent…',
'Người giữ phe Cursermancer đang chọn vai trò bí mật.':'The Cursermancer player is choosing secret roles.','Đang chờ đối phương chọn.':'Waiting for the opponent to choose.',
'Chọn 1 lá; các nhóm còn lại được chọn ngẫu nhiên':'Choose 1 card; the remaining groups are drafted randomly',
'Đã rời phòng.':'You left the room.','Đang kết nối Photon Fusion…':'Connecting to Photon Fusion…','Thiếu hoặc sai Photon Fusion App ID trong config.':'Missing or invalid Photon Fusion App ID in the configuration.',
'Chờ chủ phòng bắt đầu ván tiếp theo.':'Waiting for the host to start the next match.',
'Đang đồng bộ bộ bài trung lập…':'Synchronizing the neutral draft…',
'Đã tạo phòng. Gửi mã cho người thứ hai.':'Room created. Share the code with the second player.',
'Đã vào phòng. Chờ chủ phòng bắt đầu.':'Room joined. Waiting for the host to start.',
'Trạng thái quá lớn để gửi.':'The game state is too large to send.',
'Đã bỏ qua dữ liệu mạng không hợp lệ.':'Invalid network data was ignored.',
'Runner chưa hoạt động hoặc máy này không có quyền chuyển scene.':'The network runner is not ready or this player cannot change scenes.',
'Chưa có Photon Fusion 2 SDK. Import SDK rồi chờ Unity biên dịch; App ID đã được lưu sẵn.':'Photon Fusion 2 SDK is missing. Import the SDK and wait for Unity to compile. The App ID is already saved.',
'Không thể đồng bộ scene: Photon Fusion 2 chưa được biên dịch.':'Cannot synchronize scenes: Photon Fusion 2 has not compiled.',
'Mã phòng gồm 4–20 ký tự A–Z, 0–9 hoặc dấu -.':'Room codes use 4–20 characters: A–Z, 0–9 or hyphens.',
'Đang chuyển cả hai máy vào bàn chơi (Fusion)...':'Loading the shared board on both players…','Host đã bắt đầu. Đang tải bàn chơi (Fusion)...':'The host has started. Loading the shared board…',
'Đã chuyển sang bàn chơi (Fusion).':'Shared board loaded.','Đối phương đã rời phòng. Trận đã dừng; hãy về Menu.':'The opponent left. The match has stopped; return to Menu.',
'Chủ phòng đã rời. Phiên này không tự chuyển chủ phòng; hãy tạo phòng mới.':'The host left. This session does not transfer hosting; create a new room.',
'Kết nối đang chậm. Các thao tác đã được giữ lại, vui lòng đợi một chút.':'The connection is slow. Your queued actions are retained; please wait a moment.',
'Lượt đã thay đổi. Hãy thao tác lại.':'The turn changed. Please try again.','Thao tác này không hợp lệ trong lượt hiện tại.':'This action is not available during the current turn.',
'Ghế này không còn tồn tại.':'This seat no longer exists.','Mục tiêu đã thay đổi. Hãy chọn lại lá bài.':'The target changed. Select the card again.',
'Hãy chọn xong bài trung lập trước.':'Finish the neutral draft first.','Không thực hiện được thao tác. Hãy chọn lại mục tiêu.':'Could not perform the action. Select the target again.',
'Chỉ chọn vai trò cho lá còn sống trong khay bài.':'Select roles only for living cards in a hand.','Phiếu phải nhắm vào một lá còn sống khác trên bàn.':'Vote for a different living card on the board.',
'Hãy bỏ phiếu từ một lá thuộc phe của bạn đang ở trên bàn.':'Cast a vote from an on-board card of your faction.',
'Lựa chọn hồi sinh đã thay đổi. Hãy mở lại danh sách.':'Resurrection choices changed. Open the list again.',
'Lá bài chưa ở trên bàn hoặc không có kỹ năng chủ động phù hợp.':'The card is not on the board or has no suitable active skill.',
'Lá ở ghế đích đang bị khóa, chưa thể đổi chỗ.':'The card in the destination seat is locked and cannot be swapped yet.',
'Hãy bỏ phiếu vào một lá còn sống trên bàn.':'Vote for a living card on the board.','Thợ săn số 9 không thể trở thành Cursermancer.':'Hunter number 9 cannot become a Cursermancer.',
'Hãy hoàn tất chọn Cursermancer theo từng nhóm bài trước khi bắt đầu.':'Finish selecting Cursermancers in each faction before starting.',
'PHE DÂN LÀNG THẮNG! (Lời nguyền đã bị dập tắt)':'VILLAGERS WIN! (The curse has ended)',
'PHE CURSERMANCER THẮNG! (Bóng tối bao trùm ngôi làng)':'CURSERMANCER WINS! (Darkness consumes the village)',
'Bài hướng dẫn thiếu dữ liệu. Chọn Về menu, hoặc sửa FirstMatchLesson trong Inspector.':'Tutorial data is missing. Return to Menu or repair FirstMatchLesson in the Inspector.'}
for vn,en in ui.items():vi(vn,en)
templates={
'Đấu máy · Cấp {0} · {1} Cursermancer\nOnline luôn có 2 Cursermancer; cấp độ chỉ áp dụng khi đấu máy.':'Bot match · Level {0} · {1} Cursermancer(s)\nOnline always uses 2 Cursermancers; difficulty affects bots only.',
'Phòng {0} · {1}/2 người · {2}':'Room {0} · {1}/2 players · {2}',
'HIẾN TẾ · {0}':'SACRIFICE · {0}','Đã bỏ phiếu {0}/{1} · Kéo từ mỗi lá của bạn tới một lá khác trên bàn.':'Voted {0}/{1} · Drag from each of your cards to a different card on the board.',
'{0}: {1}/{2}':'{0}: {1}/{2}',
'Thanh trừng ({0}/2)':'Purge ({0}/2)','{0}:  {1}/{2}':'{0}:  {1}/{2}',
'HƯỚNG DẪN · {0}/{1} · {2}':'TUTORIAL · {0}/{1} · {2}','BƯỚC {0} / {1}':'STEP {0} / {1}',
'Lượt {0}/{1} · Người chơi {2} chọn · {3}':'Pick {0}/{1} · Player {2} chooses · {3}',
'Chưa bỏ phiếu: {0}. Kéo từ các lá này tới lá muốn hiến tế.':'Still to vote: {0}. Drag from these cards to the sacrifice target.',
'Chưa bỏ phiếu: {0}.':'Still to vote: {0}.','{0} (số {1})':'{0} (number {1})',
'Không đủ tim cho {0} (cần {1} tim).':'Not enough hearts for {0} ({1} hearts required).',
'Không đủ tim để thu hồi {0} (cần {1} tim).':'Not enough hearts to return {0} ({1} hearts required).',
'{0} đang bị khóa hoặc không còn trên bàn.':'{0} is locked or is no longer on the board.',
'{0} đang bị khóa hoặc ghế không hợp lệ.':'{0} is locked or the seat is invalid.',
'{0} không thể tự bỏ phiếu cho chính mình.':'{0} cannot vote for itself.',
'Không thể dùng kỹ năng của {0}. Kiểm tra mục tiêu, số tim và điều kiện trong mô tả bài.':'Cannot use {0}\'s skill. Check the target, hearts and conditions in its description.',
'Nhóm bài này cần {0} Cursermancer. Bấm lại lá đã chọn để bỏ chọn trước khi đổi.':'This faction needs {0} Cursermancer(s). Click a selected card again to deselect it before changing.',
'Kết nối đã đóng: {0}':'Connection closed: {0}','Mất kết nối: {0}':'Disconnected: {0}','Không kết nối được: {0}':'Could not connect: {0}',
'Không vào được phòng: {0}':'Could not join the room: {0}','Lỗi Fusion: {0}':'Fusion error: {0}',
'Fusion không tải được scene: {0}':'Fusion could not load the scene: {0}',
'{0} · Đang chờ đối phương chọn.':'{0} · Waiting for the opponent to choose.',
'Phe ván sau: {0}':'Next match side: {0}','Chơi tiếp ở cấp {0}':'Play again at level {0}'}
for vn,en in templates.items():vi(vn,en)
rules=next(r for r in rows if r['key']=='MenuScenes.Rules Text — edit wording here')
rule_en='''OBJECTIVE
Villagers win when every Cursermancer is eliminated. Cursermancer wins when living Cursermancers equal or outnumber living non-Cursermancer Villagers. Neutral Cursermancers also count.

1. ACTIONS
Sides alternate turns with 5 hearts. Play your faction's card: 2 hearts. Opponent or neutral card: 5 hearts. Totem and other skills have their own costs.

2. INFECTION
A full board or Reeve begins the ritual. Cursermancer targets a card in the same row or column. Empty seats block infection.

3. SACRIFICE
Each on-board card of the voting faction chooses a different card. No self-votes. A sole card on the board is exempt. The most-voted cards are sacrificed; a tie sacrifices all tied cards. Skills can change the result.

PURGE
Each side has 2 uses per match. Destroys infected cards in hands and on the board. Available during action turns only.

RETURN TO HAND
Returning your card or a neutral card refunds up to 2 hearts, never more than its placement cost. Totem refunds only 1 heart.

CONTROLS
Left-click to select or drag cards. Right-click to inspect. Each card's description explains its skills and costs. ESC closes Rules and resumes play.'''
pair(rules['source'],rule_en,rules['source'],rules['key'])
pair('<color=yellow><b>SUSPICION MARK:</b></color>\nThis card is under suspicion by the Detective. Each stacked hat increases the target\'s suspicion level.',
     '<color=yellow><b>SUSPICION MARK:</b></color>\nThis card is under suspicion by the Detective. Each stacked hat increases the target\'s suspicion level.',
     '<color=yellow><b>DẤU NGHI NGỜ:</b></color>\nLá này đang bị Detective nghi ngờ. Mỗi chiếc mũ chồng thêm làm tăng mức Nghi ngờ của mục tiêu.')

# Safe line-wise localization handles composed headers without changing their wire representation.
for source in ['THEREASM','Online','Menu','Cursermancer','→','1','2','3','4','5','5/5']:
    pair(source,source,source)
pair('TAY BÀI CỦA BẠN','YOUR HAND','TAY BÀI CỦA BẠN','ownership.yourHand')
pair('TAY BÀI ĐỐI THỦ',"OPPONENT'S HAND",'TAY BÀI ĐỐI THỦ','ownership.otherHand')
pair('BÀI TRUNG LẬP','NEUTRAL CARDS','BÀI TRUNG LẬP','ownership.neutralHand')
pair('Cả hai phe có thể sử dụng','Either side can use these cards','Cả hai phe có thể sử dụng','ownership.shared')
pair('Nhóm bài gốc · không phải vai trò bí mật','Printed faction · secret roles stay hidden','Nhóm bài gốc · không phải vai trò bí mật','ownership.publicFaction')
story_path=Path(__file__).parent/'tutorial_story.json'
if story_path.exists():
    story=json.loads(story_path.read_text(encoding='utf-8'))
    for stage,(vn,en) in enumerate(story['purposes']):pair(vn,en,vn,'guided.'+str(stage)+'.purpose')
    for stage,(vn,en) in enumerate(story.get('successes',[])):pair(vn,en,vn,'guided.'+str(stage)+'.success')
    for stage,fields in story['overrides'].items():
        for field,(vn,en) in fields.items():pair(vn,en,vn,'guided.'+stage+'.'+field)
    for i,(vn,en) in enumerate(story['ui']):pair(vn,en,vn,'guided.ui.'+str(i))
# Preserve the authored workshop translations when rebuilding the full catalog.
saved_catalog=Path(__file__).parent/'catalog.json'
if saved_catalog.exists():
    for entry in json.loads(saved_catalog.read_text(encoding='utf-8'))['entries']:
        if entry['key'] in ['guided.heartOverview','guided.movementCosts','guided.swapCosts','guided.skillCosts','guided.inspection']:
            pair(entry['source'],entry['english'],entry['vietnamese'],entry['key'])
missing=[]
for r in rows:
    s=r['source']
    if not s or s in entries:continue
    if all(line in entries or not line.strip() for line in s.split('\n')):continue
    if re.fullmatch(r'[\d\s/]+',s):continue
    missing.append((r['key'],s))
(Path(__file__).parent/'missing.json').write_text(json.dumps(missing,ensure_ascii=False,indent=2),encoding='utf-8')
(Path(__file__).parent/'catalog.json').write_text(json.dumps({'entries':list(entries.values())},ensure_ascii=False,indent=2),encoding='utf-8')
print('Authored',len(entries),'entries; unmatched exported strings:',len(missing))

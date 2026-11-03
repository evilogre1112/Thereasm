"""Bilingual copy for the vote audit and compact status UI; preserve the rest of the catalog."""
from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[2]
pairs=[
("Players' ballots","Phiếu của người chơi"),
("These are the players' original ballots. Neutral effects are applied next.","Đây là phiếu gốc do người chơi bỏ. Tiếp theo, các lá Neutral sẽ tác động lên kết quả này."),
("Trader divides the highest count ({0}) among {1} occupied seats: {2} votes each. Remainder discarded.","Trader lấy số phiếu cao nhất ({0}), chia cho {1} ghế có người: mỗi lá còn {2} phiếu. Làm tròn xuống, bỏ phần dư."),
("Jester scatters extra votes at random. Only the cards shown in this step receive them.","Jester tung thêm phiếu ngẫu nhiên. Những lá được liệt kê ở bước này nhận thêm phiếu; các lá khác giữ nguyên."),
("Snooper adds one vote to its indicated target. If no valid target is available, no vote is added.","Snooper cộng một phiếu vào lá bị chỉ điểm. Nếu không có mục tiêu hợp lệ, lần này không cộng phiếu."),
("The first bettor wins: no counter-bet was placed. Add 1 vote to the chosen target.","Người bỏ phiếu trước thắng cược vì người sau không cược. Mục tiêu đã chọn được cộng 1 phiếu."),
("The second bettor wins because both players bet. Add 1 vote to the chosen target.","Cả hai cùng cược nên người bỏ phiếu sau thắng cược. Mục tiêu đã chọn được cộng 1 phiếu."),
("The first bettor loses because both players bet. Remove 1 vote from that player's recorded ballot target, if possible.","Cả hai cùng cược nên người bỏ phiếu trước thua cược. Bớt 1 phiếu ở mục tiêu lá phiếu gốc của người đó, nếu còn phiếu để bớt."),
("The second bettor loses: only the second player bet. Remove 1 vote from that player's recorded ballot target, if possible.","Chỉ người bỏ phiếu sau cược nên người đó thua cược. Bớt 1 phiếu ở mục tiêu lá phiếu gốc của họ, nếu còn phiếu để bớt."),
("Sacrifier takes the sacrifice itself. The displayed votes stay unchanged; they no longer choose the victim.","Sacrifier nhận hiến tế thay mọi lá khác. Số phiếu vẫn được giữ để bạn xem, nhưng không còn quyết định ai chết."),
("Controller changes the verdict: cards with the lowest positive vote count are sacrificed, instead of the highest count.","Controller đổi luật phán quyết: lá có số phiếu thấp nhất nhưng lớn hơn 0 bị hiến tế, thay cho lá nhiều phiếu nhất."),
("Detective adds 1 suspicion here. Suspicion guides its later comparisons; it is not an extra vote.","Detective đặt thêm 1 điểm nghi vấn lên lá này. Điểm đó dùng cho những lần so sánh sau của Detective, không phải phiếu bầu."),
("Your wager is selected. The other player's wager remains hidden until the tally.","Bạn đã chọn mục tiêu cược. Lựa chọn cược của đối phương vẫn được giữ kín tới lúc tổng kết."),
("Your Snooper target is selected. This selection stays private until the tally.","Bạn đã chọn mục tiêu cho Snooper. Lựa chọn này được giữ riêng cho bạn tới lúc tổng kết."),
("This card just activated its skill.","Lá này vừa kích hoạt kỹ năng."),
("votes","phiếu"),
("suspicion","nghi vấn"),
("No vote count changed in this step.","Không lá nào đổi số phiếu ở bước này."),
("Chosen for sacrifice","Được chọn hiến tế"),
("VOTE RESOLUTION","DIỄN BIẾN PHIẾU"),
("CARD EFFECT DETAILS","CHI TIẾT HIỆU ỨNG BÀI"),
("Suspicion is not a vote. Detective uses it when comparing neighbours; it does not directly increase this card's ballots.","Nghi vấn không phải phiếu bầu. Detective dùng điểm này để so sánh hai lá bên cạnh, không trực tiếp cộng phiếu vào lá này."),
("Only your wager is shown before the tally. Counter-bets are resolved together when both players finish voting.","Trước tổng kết, bạn chỉ thấy cược của mình. Cược hai bên được xử lý cùng nhau sau khi cả hai bỏ phiếu xong."),
("Selections stay private before the tally. Snooper's actual vote target is public when its effect resolves.","Lựa chọn được giữ kín trước tổng kết. Lá thực sự nhận phiếu từ Snooper sẽ được công khai khi kỹ năng được xử lý."),
("During voting, only your own ballots are visible. Both sides and Neutral effects are revealed at the tally.","Khi đang bỏ phiếu, bạn chỉ thấy phiếu của mình. Phiếu hai bên và tác động của Neutral được công khai lúc tổng kết."),
("Original ballots → Neutral effects → Verdict. Click a vote badge to inspect that card.","Phiếu gốc → Tác động của Neutral → Phán quyết. Bấm huy hiệu phiếu để xem riêng từng lá."),
("No public vote changes yet. Cast your votes; the tally will explain every Neutral effect here.","Chưa có thay đổi phiếu được công khai. Cứ bỏ phiếu nhé; khi tổng kết, từng tác động của Neutral sẽ được giải thích ở đây."),
("Vote details","Chi tiết phiếu"),
("Replay","Xem lại"),
("Close","Đóng"),
("Step {0} · {1}","Bước {0} · {1}"),
("Vote details · {0} steps","Chi tiết phiếu · {0} bước"),
("{0}: suspicion +{1}, not votes. Click this seal for details.","{0}: thêm {1} điểm nghi vấn, không phải phiếu. Bấm huy hiệu này để xem giải thích nhé."),
("This seal marks your wager target. Click to read how Gambler resolves the bet.","Dấu này chỉ mục tiêu cược của bạn. Bấm để đọc cách Gambler xử lý thắng và thua cược nhé."),
("This eye marks Snooper's indicated target. Click for its vote effect; private selections stay hidden from the other player.","Con mắt chỉ mục tiêu của Snooper. Bấm để xem tác động lên phiếu; lựa chọn riêng vẫn được giấu với đối phương."),
("{0}: {1} votes currently shown. Click for where these votes came from.","{0}: hiện có {1} phiếu đang được hiển thị. Bấm để xem các phiếu này đến từ đâu nhé."),
("Selected target","Mục tiêu đã chọn"),
("LAST TALLY","LẦN TỔNG KẾT GẦN NHẤT"),
]
asset=ROOT/'Assets/UI/Localization/GameLocalization.asset';text=asset.read_text(encoding='utf-8-sig')
path=ROOT/'Tools/Localization/catalog.json';data=json.loads(path.read_text(encoding='utf-8'))
for i,(en,vi) in enumerate(pairs):
    key=f'votes.presentation.{i:02d}';entry=dict(key=key,source=en,english=en,vietnamese=vi)
    block='  - key: '+json.dumps(key)+'\n'+''.join('    '+f+': '+json.dumps(entry[f],ensure_ascii=False)+'\n' for f in ['source','english','vietnamese'])
    pattern=rf'(?ms)^  - key: "?{re.escape(key)}"?\n.*?(?=^  - key:|\Z)'
    if re.search(pattern,text):text=re.sub(pattern,lambda _:block,text)
    else:text=text.rstrip()+'\n'+block
    old=next((n for n,e in enumerate(data['entries']) if e['key']==key),None)
    if old is None:data['entries'].append(entry)
    else:data['entries'][old]=entry
asset.write_text(text,encoding='utf-8');path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Authored {len(pairs)} bilingual vote explanations.')

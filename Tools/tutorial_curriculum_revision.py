"""Action-first revision. Exported courses contain no quiz or separate Decay lesson.

The learner switches the presentation perspective, never grants Village secret data.
All outcomes are described in a following observation beat, after the real action.
"""
def revise(ctx):
    A=ctx['A'];O=ctx['O'];A.extend(['View','Wager']);O.append('ResetTable')
    c=ctx['copy'];op=ctx['op'];phase=ctx['phase'];hearts=ctx['hearts'];b=ctx['beat'];read=ctx['read'];place=ctx['place'];vote=ctx['vote'];skill=ctx['skill'];end=ctx['end'];tally=ctx['tally'];inspect=ctx['inspect']
    old={x['id']:x for x in ctx['lessons']}
    result=[]
    def make(id,chapter,title,summary,steps,setup=None,focus=None):
        # Use the established card aliases/number collision protection.
        item=ctx['lesson'](id,chapter,title,summary,steps,setup,focus)
        result.append(item);return item
    def see(role,en,vi,prepare=None):
        return b('View','Press '+('Cursermancer view.' if role==0 else 'Village view.'),
            'Bấm '+('Góc nhìn Cursermancer.' if role==0 else 'Góc nhìn Village.'),en,vi,seat=role,prepare=prepare,
            title=('Step into their shoes','Thử đứng ở phía bên kia'))
    def observe(en,vi,prepare=None,title=('Look at the table now','Nhìn lại bàn chơi nhé')):
        return read(title,en,vi,prepare)
    def attempt(action,n,en,vi,afterEn,afterVi,seat=-1,target=0,prepare=None):
        beat=b(action,en,vi,'Give it a try.','Mình thử một lần nhé.',source=n,target=target,seat=seat,prepare=prepare,
            title=('What happens if you try?','Thử xem chuyện gì xảy ra'))
        beat['success']=c(afterEn,afterVi);return beat
    def dead(open=True):
        return b('OpenCemetery' if open else 'CloseCemetery','Open Cemetery.' if open else 'Press Close in Cemetery.',
            'Bấm Nghĩa trang.' if open else 'Bấm Đóng trong Nghĩa trang.',
            'Take a closer look at the result.' if open else 'Return to the table.',
            'Mình nhìn gần hơn kết quả nhé.' if open else 'Trở lại bàn chơi nhé.')
    def p(n,s,prepare=None):return place(n,s,'This action spends hearts.','Thao tác này dùng tim.',prepare)
    def reset():return [op('ResetTable'),phase(),hearts(),op('Hearts',value=5)]

    make('controls-first-night',0,('Take your seat at the table','Làm chủ bàn chơi'),
        ('Read, place, retrieve, aim Hunter, spread infection, vote and Purge on one table.',
         'Tự xem, đặt, rút bài, nhắm Hunter, lây nhiễm, bỏ phiếu và thanh trừng trong một bài.'),[
        observe('Welcome to the village. Seven seats, a hand of cards, and a little time to find your feet.',
                'Chào bạn đến với ngôi làng. Có bảy chiếc ghế và một tay bài đang chờ. Mình làm quen từng chút nhé.'),
        *inspect(10),p(10,0),
        attempt('BlockedReturn',10,'Try LEFT-dragging number 10 back to your hand.','Thử kéo CHUỘT TRÁI lá số 10 trở về tay.',
                'Number 10 was just played. It must stay until your next action turn.',
                'Lá số 10 vừa được đặt xuống. Nó cần ở lại đến lượt hành động kế tiếp của bạn.'),
        end(),end(),
        b('Return','LEFT-drag number 10 back to your hand.','Kéo CHUỘT TRÁI lá số 10 về tay.',
          'Your action turn has returned. Try the same gesture again.','Đã trở lại lượt của bạn. Thử lại chính động tác lúc nãy nhé.',source=10),
        observe('That time it came home. You can now move it or choose a new place for it.',
                'Lần này lá bài đã về tay. Bạn đã biết cách đón nó về rồi đấy.'),
        skill(9,1,prepare=[op('Place',9,1),op('Place',1,2),op('Cur',1),hearts()]),
        dead(),dead(False),
        see(0,'Come over to Cursermancer for the next moment.','Sang phía Cursermancer một nhịp để thử nghi thức nhé.',
            prepare=reset()+[op('Place',1,2),op('Place',10,0),op('Place',11,1),op('Cur',1),phase(3,0)]),
        b('Curse','LEFT-drag number 1 onto number 11.','Kéo CHUỘT TRÁI từ lá số 1 sang lá số 11.',
          'Leave a mark on the neighboring card.','Để lại một dấu bệnh trên lá bên cạnh.',source=1,target=11,seat=1),
        observe('Number 11 now carries infection. You are seeing this from Cursermancer.',
                'Lá số 11 đã mang bệnh. Bạn đang nhìn bằng mắt của Cursermancer.'),
        see(1,'Now look from Village.','Giờ mình trở về góc nhìn Village.'),
        observe('The same number 11 looks ordinary from here. Remember its seat.',
                'Vẫn lá số 11 ấy, nhìn từ đây lại như bình thường. Nhớ chỗ nó đang ngồi nhé.'),
        vote(10,1,2,prepare=[phase(4),op('Vote',1,0)]),vote(11,1,2),tally(),
        observe('The ballots are resolved. The card with the highest total went to the cemetery.',
                'Phiếu đã được chốt. Lá có tổng phiếu cao nhất vừa vào nghĩa trang.'),
        see(0,'Visit Cursermancer once more before Purge.','Qua Cursermancer một nhịp trước khi thanh trừng.',
            prepare=reset()+[op('Place',1,0),op('Place',10,1),op('Cur',1),op('Infect',10,1),op('Infect',11,1)]),
        observe('Number 10 is infected on the table; number 11 is infected in your hand. The infection labels belong to this viewpoint.',
                'Lá số 10 bị nhiễm trên bàn; lá số 11 bị nhiễm trong tay. Ở góc nhìn này, bạn thấy dấu bệnh của chúng.'),
        see(1,'Return to Village before acting.','Quay lại Village rồi mình mới hành động.'),
        b('Purge','Press Purge.','Bấm Thanh trừng.','See what this button finds.','Thử xem nút này tìm được gì nhé.'),
        dead(),observe('Both infected cards were removed. Number 1, the Cursermancer without infection, remains seated.',
                'Hai lá mang bệnh đã bị loại. Lá số 1 là Cursermancer nhưng không mang bệnh, nên vẫn còn trên ghế.'),dead(False)])

    make('energy-empty',1,('Make the most of five hearts','Dùng trọn năm tim'),
        ('Spend, run out, refill, retrieve, move and swap in one continuous lesson.',
         'Đặt bài, hết tim, hồi tim, rút bài, dời chỗ và đổi bài trong cùng một bài học.'),[
        observe('Hearts are the energy you spend on actions. Each new action turn starts with five.',
                'Tim là năng lượng để bạn hành động. Mỗi lượt hành động mới, bạn có năm tim.'),
        p(10,0),observe('Playing your own card used two hearts. Three remain.',
                      'Đặt bài của mình vừa dùng hai tim. Bạn còn ba tim.'),
        p(11,1),observe('Another two hearts spent. There is one left.',
                      'Lại dùng thêm hai tim. Bây giờ còn một tim.'),
        attempt('BlockedPlace',12,'Try placing number 12 in seat 3.','Thử đặt lá số 12 vào ghế số 3.',
                'That placement needs two hearts. You have one, so the card returned without spending anything.',
                'Lần đặt này cần hai tim mà bạn chỉ còn một. Lá bài trở về tay, tim vẫn giữ nguyên.',seat=2),
        end(),end(),observe('Five hearts again. A new action turn gives you a fresh budget.',
                           'Năm tim đã trở lại. Đến lượt hành động mới là bạn có sức để bắt đầu tiếp.'),
        b('Return','LEFT-drag number 10 back to hand.','Kéo CHUỘT TRÁI lá số 10 về tay.',
          'Try bringing your own card home.','Thử đón lá của mình về tay.',source=10),
        p(3,0,prepare=[op('Return',11),hearts()]),
        observe('That card belongs to the other printed faction. Placing it spent all five hearts.',
                'Lá ấy thuộc nhóm bài phía bên kia. Đặt nó xuống vừa dùng hết năm tim.'),
        attempt('BlockedPlace',12,'Try placing number 12 in seat 2.','Thử đặt lá số 12 vào ghế số 2.',
                'No hearts remain to pay for this placement. End the turn to get a new budget.',
                'Bạn đã hết tim để đặt thêm bài. Mình kết thúc lượt để đón lượt mới nhé.',seat=1),
        observe('Let us try movement with two cards already seated.','Giờ mình thử dời hai lá đã có sẵn trên bàn nhé.',
            prepare=reset()+[op('Place',10,0),op('Place',3,5)]),
        p(10,1),observe('Moving your own seated card cost one heart.', 'Dời lá của mình trên bàn vừa tốn một tim.'),
        p(3,4),observe('Moving the opposing card cost three hearts.', 'Dời lá phía đối phương vừa tốn ba tim.'),
        observe('A fresh table for one last gesture: a swap.','Bàn mới cho một động tác cuối: đổi chỗ.',
            prepare=reset()+[op('Place',3,0)]),
        b('Swap','LEFT-drag number 12 onto number 3.','Kéo CHUỘT TRÁI lá số 12 thả lên lá số 3.',
          'One card takes the seat while the other comes home.','Một lá nhận ghế, một lá trở về tay.',source=12,target=3,seat=0),
        observe('The opposing card returned to its hand. Your card took its place, for five hearts in total.',
                'Lá đối phương đã về tay của nó, lá của bạn nhận chỗ ngồi. Cả lần đổi vừa dùng năm tim.')])

    # Keep every original ability, with its result discussed only after the action.
    special={'card-snooper','card-gambler','card-blind','card-witch'}
    for id,item in old.items():
        if item['chapter']!=2 or id in special or id in {'card-snooper-decay','card-gambler-counter'}:continue
        item=dict(item);steps=[]
        for step in item['beats']:
            action=A[step['action']]
            if action=='Quiz':continue
            # Full instructions remain accessible through right-click, with no advance lecture.
            if action=='Read' and not steps:continue
            step=dict(step)
            if action=='BlockedReturn':
                step['success']=c('A neighboring ability is holding this card in place. The card and your hearts stay where they were.',
                                  'Kỹ năng đang giữ lá này tại chỗ. Bài và số tim của bạn vẫn giữ nguyên.')
            if action=='BlockedSkill':
                step['success']=c('Saboteur is silencing Hunter beside it. The shot did not fire.',
                                  'Saboteur đang khóa kỹ năng Hunter bên cạnh. Phát bắn chưa được thực hiện.')
            # No projected outcomes, caveats, or exceptions before trying.
            step['purpose']=c('Watch the highlighted card.','Để ý lá đang được chỉ sáng nhé.')
            if action=='Read':continue
            steps.append(step)
        name=item['title']['english'].split(' · ')[0]
        desc=next(x for x in ctx['studies'] if x[0].replace('VillageChief','Reeve').replace('GameMaster','Game Master')==name)
        # Explanation appears only after the real effect; split longer descriptions into digestible beats.
        en=desc[2].replace('VillageChief','Reeve').replace('GameMaster','Game Master')
        vi=desc[3].replace('VillageChief','Reeve').replace('GameMaster','Game Master')
        steps.append(observe(en,vi))
        item['beats']=steps;result.append(item)

    make('card-snooper',2,('Snooper · follow both whispers','Snooper · nghe từ hai phía'),
        ('Watch healthy Snooper resolve, then play its Decay choice and compare both viewpoints.',
         'Xem Snooper khỏe hoạt động, tự chọn mục tiêu Decay rồi nhìn từ cả hai phe.'),[
        observe('Snooper is seated between the two factions. First, let it watch a normal vote.',
                'Snooper đã ngồi giữa hai phe. Trước hết, để nó theo dõi một cuộc bỏ phiếu nhé.'),
        vote(10,1,0,prepare=[phase(4),op('Vote',1,2)]),tally(),
        observe('Snooper added a ballot to the seated Cursermancer. Its eye was shown to both players as the tally resolved.',
                'Snooper vừa thêm phiếu vào Cursermancer trên bàn. Khi tổng kết, con mắt ấy hiện cho cả hai người chơi.'),
        see(0,'Now visit Cursermancer on a fresh table.','Giờ qua Cursermancer trên một bàn mới.',
            prepare=reset()+[op('Place',1,0),op('Place',22,1),op('Place',10,2),op('Cur',1),op('Infect',22,1),phase(4,0)]),
        observe('Snooper is infected this time. From here you can see the infection and choose where its vote will go.',
                'Lần này Snooper đã mang bệnh. Từ đây bạn thấy bệnh và có thể chọn nơi phiếu của nó sẽ đến.'),
        skill(22,10),observe('Your eye is on number 10. Keep that choice in mind.','Con mắt của bạn đã nằm trên lá số 10. Nhớ lựa chọn này nhé.'),
        see(1,'Take a look from Village before the tally.','Nhìn từ Village trước khi chốt phiếu.'),
        observe('From Village, Snooper looks unused and the chosen eye is hidden. This is what your opponent sees.',
                'Ở phía Village, Snooper trông như chưa dùng kỹ năng và không hiện con mắt đã chọn. Đó là màn hình của đối thủ lúc này.'),
        see(0,'Switch back to your private choice.','Quay lại nhìn lựa chọn riêng của Cursermancer.'),
        observe('The mark is still here. Switching views never changes the chosen target.',
                'Dấu vẫn còn đây. Đổi góc nhìn chỉ đổi thông tin được thấy, mục tiêu đã chọn vẫn giữ nguyên.'),
        vote(1,10,2),vote(10,1,0,prepare=[phase(4)]),tally(),
        observe('Now the selected target was shown during resolution. Compare it with the healthy Snooper you saw earlier.',
                'Đến khi phân giải, mục tiêu đã chọn mới được công khai. Bạn vừa thấy cả Snooper khỏe lẫn Snooper bị Decay trên cùng một bài học.')],
        setup=[op('Place',1,0),op('Place',22,1),op('Place',10,2),op('Cur',1)],focus='Snooper22')

    def wager():
        return b('Wager','RIGHT-drag Gambler onto a target to bet, or press Pass.',
            'Kéo CHUỘT PHẢI từ Gambler sang mục tiêu để cược, hoặc bấm Bỏ qua.',
            'The opponent’s move is fixed. Choose only for your own side.',
            'Nước của đối thủ đã được đặt sẵn. Bạn chỉ quyết định cho phía mình.',source=24,target=1,
            title=('Your wager, your choice','Cược hay không, bạn chọn nhé'))
    def wagerResult(betEn,betVi,passEn,passVi):
        step=observe('The tally has resolved your choice.','Phiếu đã phân giải lựa chọn của bạn.')
        step['betResult']=c(betEn,betVi);step['passResult']=c(passEn,passVi);return step
    make('card-gambler',2,('Gambler · dare you answer?','Gambler · bạn có dám đáp cược?'),
        ('See a fixed opponent’s wager, then choose to bet or pass for your own side.',
         'Nhìn nước cược đặt sẵn của đối thủ, rồi tự chọn cược đáp hay bỏ qua cho phe mình.'),[
        observe('You are Village, voting second. In this practice scenario, Cursermancer has already bet on number 10.',
                'Bạn là Village, bỏ phiếu sau. Trong tình huống luyện này, Cursermancer đã cược vào lá số 10.'),
        see(0,'Take one look at the opponent’s prepared bet.','Nhìn một nhịp vào nước cược được đặt sẵn của đối thủ.'),
        observe('The wager mark is on number 10. This is the opponent’s choice, already made for this scenario.',
                'Dấu cược nằm trên lá số 10. Đây là lựa chọn của đối thủ đã được đặt sẵn cho tình huống này.'),
        see(1,'Return to your own side before choosing.','Về phía mình rồi bạn mới quyết định nhé.'),
        wager(),vote(10,1,0),tally(),
        wagerResult('You answered with a bet. Both sides bet: your second wager won; the first player lost a normal ballot.',
            'Bạn đã cược đáp. Cả hai cùng cược: bạn đi sau nên thắng cược; người trước bị trừ một phiếu thường.',
            'You passed. With no counter-bet, the first player won one extra ballot on number 10.',
            'Bạn đã bỏ qua. Không có cược đáp, người đi trước được thêm một phiếu vào lá số 10.'),
        observe('Another prepared situation: Cursermancer voted but did not bet. You are still Village, voting second.',
                'Tình huống mới: Cursermancer đã bỏ phiếu nhưng không cược. Bạn vẫn là Village, bỏ phiếu sau.',
            prepare=reset()+[op('Place',1,0),op('Place',24,1),op('Place',10,2),phase(4),op('Vote',1,2)]),
        see(0,'Look at their prepared side once.','Nhìn một nhịp phía đối thủ được đặt sẵn.'),
        observe('There is no wager mark this time.','Lần này không có dấu cược.'),
        see(1,'Return to Village and make your own decision.','Trở về Village rồi tự chọn nước của mình.'),
        wager(),vote(10,1,0),tally(),
        wagerResult('You bet alone as the second player. That wager lost and removed a ballot from your ordinary vote target.',
            'Bạn là người sau và đã cược một mình. Cược ấy thua, làm mất một phiếu ở mục tiêu bỏ phiếu thường của bạn.',
            'You passed too. Neither player bet, so Gambler left the ordinary ballots unchanged.',
            'Bạn cũng bỏ qua. Không ai cược nên Gambler giữ nguyên các phiếu thường.')],
        setup=[op('Place',1,0),op('Place',24,1),op('Place',10,2),phase(4,0),
               op('Cast',24,target=10),op('Vote',1,2),phase(4)],focus='Gambler24')

    make('card-witch',2,('Witch · see what the spell reaches','Witch · nhìn rõ nơi phép chạm tới'),
        ('Check infections as Cursermancer, then use Witch from Village and inspect the result.',
         'Nhìn bệnh từ Cursermancer, trở về Village dùng Witch rồi xem kết quả.'),[
        p(23,1),
        see(0,'Take one moment in Cursermancer’s view.','Qua góc nhìn Cursermancer một nhịp.'),
        observe('Numbers 1 and 10 are infected on the table. Number 11 carries infection in hand.',
                'Lá số 1 và số 10 mang bệnh trên bàn. Lá số 11 mang bệnh trong tay.'),
        see(1,'Return to Village to use Witch.','Trở về Village để dùng Witch.'),
        skill(23,prepare=[hearts()]),dead(),
        observe('Numbers 1 and 10 were swept from the table. The infected card in hand remained there.',
                'Lá số 1 và số 10 đã bị quét khỏi bàn. Lá bị nhiễm còn trong tay vẫn ở đó.'),dead(False),
        see(0,'Look at what remains from Cursermancer.','Nhìn phần còn lại từ Cursermancer.'),
        observe('Number 11 still has its infection. Witch’s spell reached the seated cards you just saw.',
                'Lá số 11 vẫn mang bệnh. Phép Witch vừa chạm tới các lá đang ngồi trên bàn mà bạn đã quan sát.')],
        setup=[op('Place',1,0),op('Place',10,2),op('Infect',1,1),op('Infect',10,1),op('Infect',11,1)],focus='Witch23')

    make('card-blind',2,('Blind · a report seen from two sides','Blind · một lời báo, hai góc nhìn'),
        ('Compare a healthy report and a Decay report, with the actual truth visible only to Cursermancer.',
         'Đối chiếu lời báo lúc khỏe và lúc Decay; Cursermancer nhìn thấy sự thật.'),[
        p(19,6),dead(),observe('Blind has inspected this death. Read the result from Village first.',
                              'Blind vừa kiểm tra lần chết này. Trước hết, đọc kết quả từ Village nhé.'),
        see(0,'Compare the same corpse from Cursermancer.','Đối chiếu cùng lá đã chết từ Cursermancer.'),
        observe('The two views agree while Blind is healthy.','Khi Blind khỏe, hai góc nhìn đang khớp nhau.'),dead(False),
        see(0,'Stay with Cursermancer for the next infection.','Qua Cursermancer để nhìn lần nhiễm tiếp theo.',
            prepare=[op('Infect',19,1),op('ResetSkill',19),op('Infect',11,1),op('Place',11,2)]),
        observe('Blind and number 11 now carry infection. Watch number 11 become the next corpse.',
                'Blind và lá số 11 đang mang bệnh. Mình nhìn lá số 11 đi vào nghĩa trang tiếp theo nhé.'),
        observe('Blind has now inspected the new death.','Blind vừa kiểm tra lần chết mới.',prepare=[op('Kill',11)]),dead(),
        see(1,'Read what Village is told.','Đọc lời báo mà Village nhận được.'),
        observe('Village sees an ordinary corpse in this report.','Trong lời báo này, Village thấy một lá đã chết bình thường.'),
        see(0,'Check the truth from Cursermancer.','Kiểm tra sự thật từ Cursermancer.'),
        observe('The corpse really was infected. Cursermancer sees the truth directly; the infected Blind changed the public report.',
                'Lá ấy thật sự đã mang bệnh. Cursermancer nhìn thẳng sự thật; Blind bị nhiễm đã làm lời báo công khai đổi đi.'),dead(False)],
        setup=[op('Infect',10,1),op('Kill',10)],focus='Blind19')

    # Strategy: keep hands-on notes; turn every former quiz into an observable consequence.
    for id in ['strategy-notes','strategy-purge-trace','strategy-own-board']:
        item=dict(old[id]);steps=[]
        for step in item['beats']:
            if A[step['action']]=='Quiz':continue
            step=dict(step)
            if A[step['action']]=='Purge':
                commands=step['prepare'];step['prepare']=[]
                steps.extend([see(0,'See the real infection once before returning to Village.',
                    'Qua Cursermancer nhìn bệnh một lần rồi trở lại Village.',prepare=commands),
                    observe('The infected number 10 has returned to hand. Your notebook still holds its old seat.',
                            'Lá số 10 mang bệnh đã về tay. Sổ của bạn vẫn ghi chỗ nó từng ngồi.'),
                    see(1,'Return to Village to investigate.','Về Village để tự điều tra.')])
                step['purpose']=c('Use the observation you wrote down.','Thử dùng điều bạn đã ghi lại.')
            steps.append(step)
        if id=='strategy-purge-trace':steps.append(observe('Number 10 was in seat 2 when infection spread. Compare its old neighbors in your notebook.',
            'Lá số 10 từng ở ghế số 2 khi bệnh lây. Đối chiếu các ghế bên cạnh nó trong sổ nhé.'))
        if id=='strategy-own-board':steps.append(observe('Your side had to vote into its own cards. You have just experienced the pressure an all-one-faction board creates.',
            'Phe của bạn vừa phải bầu vào chính bài của mình. Bạn đã tự thử sức ép khi trên bàn chỉ còn một nhóm bài.'))
        item['beats']=steps;result.append(item)

    # Gesture helpers are concise. Results are observed in the following beat.
    pre={
        'Inspect':('Take a closer look at the card.','Nhìn gần hơn lá bài nhé.'),
        'CloseInspect':('Come back to the table.','Trở về bàn chơi nhé.'),
        'Skill':('Try the ability and watch its target.','Thử kỹ năng rồi nhìn mục tiêu nhé.'),
        'Vote':('Give this card a voice in the vote.','Cho lá này lên tiếng trong cuộc bỏ phiếu.'),
        'EndTurn':('Pass the turn to the other side.','Chuyển lượt sang phía bên kia.'),
        'Tally':('Watch the ballots and the card effects resolve.','Theo dõi phiếu bầu và kỹ năng được phân giải.')}
    for item in result:
        for step in item['beats']:
            action=A[step['action']]
            if action in pre:step['purpose']=c(*pre[action])
            if action!='Read':step['success']=step['success'] if action.startswith('Blocked') else c('','')
            # Use feedback from the actual action.
            if action=='Read':step['success']=c('','')
    ctx['chapters'][2]['summary']=c('One lesson per card. Compare both sides and try healthy and Decay effects together.',
        'Mỗi lá một bài. Thử cả hai phía, đối chiếu kỹ năng khỏe và Decay ngay trên cùng bàn.')
    return result

"""Final editorial pass, AFTER all scenario/turn composers.

Never attach control/energy lectures to a card experiment. Empty supporting copy
is intentional: the gesture or the observed result is often enough on its own.
"""
def contextual_copy(lessons,ctx):
    A=ctx['A'];O=ctx['O'];c=ctx['copy'];empty=c('','')
    post={
        'card-trader':('Trader shares the highest vote count across the occupied cards in its horizontal group. Shares round down.',
                       'Trader lấy số phiếu cao nhất trong nhóm ngang rồi chia đều cho các lá có mặt, làm tròn xuống.'),
        'card-jester':('When Jester activates, it scatters three extra ballots among other seated cards.',
                       'Khi kích hoạt, Jester tung ba phiếu thêm vào những lá khác trên bàn.'),
        'card-controller':('Controller makes the cards with the lowest positive vote count take the sacrifice.',
                           'Controller khiến những lá có số phiếu dương thấp nhất nhận hiến tế.'),
        'card-sacrifier':('Sacrifier takes the sacrifice itself.', 'Sacrifier nhận hiến tế thay cho những lá bị phiếu bầu nhắm tới.'),
        'card-preacher':('Preacher shields its two horizontal neighbors. The card Hunter just shot survived behind that shield.',
                         'Preacher che chở hai hàng xóm ngang. Lá vừa bị Hunter bắn vẫn sống nhờ khiên đó.'),
        'card-prankster':('Prankster pushes cards in its left trigger area to an empty seat or back to hand.',
                          'Prankster đẩy lá trong vùng bên trái sang ghế trống hoặc về tay.'),
        'card-villagechief':('Reeve has brought the table into the curse ritual early.',
                             'Reeve vừa đưa bàn chơi sang Nguyền rủa sớm.'),
        'card-totem':('Placing Totem used one heart.', 'Đặt Totem vừa dùng một tim.'),
        'card-detective':('Detective’s seal records suspicion on a neighbor. This is a suspicion mark, not a ballot.',
                          'Con dấu của Detective ghi mức nghi ngờ lên một hàng xóm. Đó là dấu nghi ngờ, không phải phiếu bầu.'),
        'card-invoker':('Invoker has brought card number 10 from the cemetery back to hand.',
                        'Invoker vừa đưa lá số 10 từ nghĩa trang về tay.')}
    for lesson in lessons:
        id=lesson['id'];name=lesson['title']['english'].split(' · ')[0]
        phase=2;voter=1;first=0
        def prepare(commands):
            nonlocal phase,voter,first
            for command in commands:
                kind=O[command['kind']]
                if kind=='ResetTable':first=0
                elif kind=='Phase':phase=command['value'];voter=command['target']
        prepare(lesson['setup'])
        result=[]
        for index,step in enumerate(lesson['beats']):
            action=A[step['action']]
            # Inspection remains optional after the Controls lesson.
            if lesson['chapter']==2 and action in ('Inspect','CloseInspect'):continue
            if id=='card-gambler' and index in (1,8):continue # duplicate descriptions of the same prepared wager
            if id in ('card-elder','card-saboteur','card-gamemaster') and action=='Read':continue # reason already follows the failed attempt
            prepare(step['prepare'])
            if action=='Read':
                step['instruction']=empty
                if id in post:step['purpose']=c(*post[id])
                step['title']=c('What '+name+' changed',name+' vừa làm gì') if lesson['chapter']==2 else step['title']
            else:
                step['purpose']=empty
            if action=='EndTurn':
                if phase==4:
                    step['title']=c('The next voter','Đến lượt bỏ phiếu tiếp theo')
                    step['instruction']=c('Press {turnButton} to hand voting to Village.',
                                           'Bấm {turnButton} để sang lượt Village bỏ phiếu.')
                    if id=='card-gambler':step['instruction']=c('Press {turnButton} to make your response as Village.',
                        'Bấm {turnButton} để đến lượt bạn đáp cược ở phe Village.')
                    voter=1-first
                elif phase==3:
                    step['title']=c('Enter Sacrifice','Sang Hiến tế')
                    step['instruction']=c('Press {turnButton} to begin voting.','Bấm {turnButton} để bắt đầu bỏ phiếu.')
                    phase=4;voter=first
                else:
                    other='Cursermancer' if phase==2 else 'Village'
                    step['title']=c(other+'’s turn','Đến lượt '+other)
                    step['instruction']=c('Press {turnButton} to pass to '+other+'.',
                                           'Bấm {turnButton} để chuyển lượt sang '+other+'.')
                    phase=1 if phase==2 else 2
            elif action=='Tally':
                step['title']=c('Resolve '+name,name+' khi chốt phiếu') if lesson['chapter']==2 else c('Resolve the vote','Chốt phiếu')
                step['instruction']=c('Press {turnButton} to see '+name+'’s effect.',
                                       'Bấm {turnButton} để xem hiệu ứng của '+name+'.') if lesson['chapter']==2 else c('Press {turnButton} to resolve the vote.','Bấm {turnButton} để tổng kết phiếu.')
                first=1-first;phase=1 if first==0 else 2
            elif action=='Place':
                if lesson['chapter']==2:
                    step['title']=c('Seat '+name,'Đặt '+name+' vào bàn')
                    if id=='card-gamemaster' and step['source']==10:
                        step['title']=c('Try the extra seat','Thử ghế phụ')
                        step['instruction']=c('LEFT-drag number 10 onto Game Master’s highlighted extra seat.',
                            'Kéo CHUỘT TRÁI lá số 10 vào ghế phụ viền xanh của Game Master.')
            elif action=='Vote':
                step['title']=c('Your ordinary ballot','Phiếu bầu thường')
                if id=='card-gambler':step['purpose']=c('Your ordinary ballot and your wager target are separate choices.',
                    'Phiếu bầu thường và mục tiêu cược là hai lựa chọn riêng.')
            elif action=='Wager':
                step['instruction']=c('RIGHT-drag Gambler onto your wager target, or press Pass.',
                    'Kéo CHUỘT PHẢI từ Gambler sang lá muốn cược, hoặc bấm Bỏ qua.')
            elif action=='Skill':
                step['title']=c('Use '+name,'Dùng '+name) if lesson['chapter']==2 else c('Aim Hunter','Nhắm bắn với Hunter')
            elif action in ('BlockedReturn','BlockedSkill','BlockedPlace'):
                step['title']=c('Try this move','Thử thao tác này')
                if id=='card-elder':step['success']=c('Elder is holding number 10 in place beside it.',
                    'Elder đang giữ lá số 10 bên cạnh, nên bạn chưa thể rút lá đó về tay.')
                elif id=='card-gamemaster':step['success']=c('Game Master cannot be moved by hand while its extra seat is occupied.',
                    'Ghế phụ đang có người nên Game Master chưa thể được di chuyển bằng tay.')
            elif action in ('OpenCemetery','CloseCemetery'):
                step['title']=c('The cemetery','Nghĩa trang')
            elif action in ('OpenNotes','CloseNotes','PlaceNote'):
                step['title']=c('Record a seat','Ghi nhớ vị trí')
            elif action=='OpenInvoker':step['title']=c('Call with Invoker','Gọi bài bằng Invoker')
            elif action=='Resurrect':step['title']=c('Bring the card back','Đưa lá bài trở về')
            # Result feedback must not resurrect generic praise on the next step.
            if not action.startswith('Blocked'):step['success']=empty
            result.append(step)
        lesson['beats']=result
        if id=='card-gambler':
            reads=[s for s in result if A[s['action']]=='Read']
            reads[0]['title']=c('Your opponent has bet','Đối thủ đã cược')
            reads[0]['purpose']=c('Cursermancer has bet on number 10. You will play Village, the second voter.',
                'Cursermancer đã cược vào lá số 10. Bạn sẽ chơi Village, người bỏ phiếu sau.')
            reads[1]['title']=c('Your wager resolved','Kết quả cược của bạn')
            reads[2]['title']=c('Your opponent passes','Đối thủ không cược')
            reads[2]['purpose']=c('This time Cursermancer has voted without betting. You still decide for Village.',
                'Lần này Cursermancer đã bỏ phiếu và không cược. Bạn vẫn quyết định cho Village.')
            reads[3]['title']=reads[1]['title']
        elif id=='strategy-notes':
            result[0]['purpose']=c('Use the notebook to remember which card sat in which seat.',
                'Dùng sổ tay để ghi nhớ lá nào từng ngồi ở ghế nào.')
        elif id=='strategy-own-board':
            result[0]['purpose']=c('Only Village cards remain seated. Try voting on this table.',
                'Trên bàn chỉ còn bài Village. Thử bỏ phiếu trên bàn này nhé.')
        read_titles={
            'card-snooper':[
                ('Healthy Snooper','Snooper lúc khỏe'),('The eye at resolution','Con mắt lúc tổng kết'),
                ('Snooper is infected','Snooper đã nhiễm'),('Your private choice','Lựa chọn bí mật của bạn'),
                ('What Village sees','Village nhìn thấy gì'),('The target becomes public','Mục tiêu được công khai')],
            'card-witch':[('Find the gray cards','Nhìn những lá xám'),('After Witch’s spell','Sau phép của Witch'),('The card still in hand','Lá còn lại trong tay')],
            'card-blind':[('Blind’s report to Village','Lời báo cho Village'),('Cursermancer sees the truth','Sự thật phía Cursermancer'),
                ('Blind is infected','Blind đã nhiễm'),('The next death','Một lá khác vừa chết'),('The truth in the cemetery','Sự thật ở nghĩa trang'),('The Decay report','Lời báo sau Decay')]}
        if id in read_titles:
            reads=[s for s in result if A[s['action']]=='Read'];assert len(reads)==len(read_titles[id])
            for step,title in zip(reads,read_titles[id]):step['title']=c(*title)
        # A setup checkpoint is not another lesson. Fold its commands into the
        # following observation rather than adding a second Continue screen.
        merged=[];i=0
        while i<len(result):
            step=result[i]
            if (A[step['action']]=='Read' and step['purpose']['english'].startswith('A new practice situation is ready.')
                and i+1<len(result) and A[result[i+1]['action']]=='Read'):
                following=result[i+1];following['prepare']=step['prepare']+following['prepare'];merged.append(following);i+=2
            else:merged.append(step);i+=1
        lesson['beats']=merged
    return lessons

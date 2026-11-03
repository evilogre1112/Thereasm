"""Use actual EndTurn transitions; never offer manual viewpoint buttons."""
def natural_turns(lessons,ctx):
    A=ctx['A'];c=ctx['copy'];op=ctx['op'];phase=ctx['phase'];end=ctx['end'];vote=ctx['vote'];skill=ctx['skill'];tally=ctx['tally'];b=ctx['beat']
    def read(en,vi,prepare=None,source=0):
        step=ctx['read'](('Look at the table now','Nhìn lại bàn chơi nhé'),en,vi,prepare);step['source']=source;return step
    def cemetery(open=True):return b('OpenCemetery' if open else 'CloseCemetery',
        'Open Cemetery.' if open else 'Press Close in Cemetery.','Bấm Nghĩa trang.' if open else 'Bấm Đóng trong Nghĩa trang.',
        'Look at the card from your current side.','Nhìn lá bài từ phe hiện tại nhé.')
    for item in lessons:
        id=item['id'];steps=item['beats']
        if id=='controls-first-night':
            result=[];purge=False
            for step in steps:
                if A[step['action']]=='View':
                    if step['prepare']:
                        if any(x['kind']==ctx['O'].index('Infect') for x in step['prepare']):
                            purge=True;step['prepare']+=[phase(1,0)]
                        result.append(read('A new practice situation is ready. You are Cursermancer now.',
                            'Tình huống mới đã sẵn sàng. Lúc này bạn đang chơi Cursermancer.',step['prepare']))
                        if not purge:result.append(read('CURSE RITUAL: Cursermancer spreads infection from a seated source. Village will need to work out where it came from.',
                            'NGUYỀN RỦA: Cursermancer lây bệnh từ một lá nguồn trên bàn. Village sẽ tìm dấu vết để lần ra nguồn lây.'))
                    elif purge:result.append(end())
                    else:result.extend([end(),read('SACRIFICE: the factions vote in order. Each of your seated faction cards points one ballot at another card.',
                        'HIẾN TẾ: hai phe lần lượt bỏ phiếu. Mỗi lá thuộc phe bạn đang ngồi trên bàn gửi một phiếu vào một lá khác.'),vote(1,10,0),end()])
                    continue
                if 'now carries infection' in step['purpose']['english']:
                    step['purpose']=c('Number 11 has turned gray: gray cards are infected. Only Cursermancer sees this change.',
                        'Lá số 11 đã chuyển xám: lá xám là lá bị nhiễm bệnh. Chỉ phe Cursermancer thấy thay đổi này.')
                if 'infection labels' in step['purpose']['english']:
                    step['purpose']=c('Number 10 is gray on the table; number 11 is gray in hand. Both carry infection.',
                        'Lá số 10 trên bàn và số 11 trong tay đều đã chuyển xám. Cả hai đang mang bệnh.');step['source']=11
                result.append(step)
            result.insert(1,read('ACTION: spend hearts to play cards, move them and use abilities. End turn passes play to the other faction.',
                'HÀNH ĐỘNG: dùng tim để đặt bài, dời chỗ và sử dụng kỹ năng. Bấm Kết thúc lượt để chuyển sang phe bên kia.'))
            item['beats']=result
        elif id=='card-snooper':
            item['beats']=[
                read('First, let healthy Snooper watch a normal vote.','Trước hết, để Snooper khỏe theo dõi một cuộc bỏ phiếu nhé.'),
                vote(10,1,0,prepare=[phase(4),op('Vote',1,2)]),tally(),
                read('Snooper added one ballot to the seated Cursermancer. Both players saw its eye during resolution.',
                    'Snooper vừa thêm một phiếu vào Cursermancer trên bàn. Khi phân giải, cả hai người chơi đều thấy con mắt ấy.'),
                read('A new round is prepared. You are Cursermancer. Snooper has turned gray: it is infected.',
                    'Tình huống mới đã được chuẩn bị. Bạn đang là Cursermancer. Snooper đã chuyển xám, nghĩa là bị nhiễm bệnh.',
                    [op('ResetTable'),op('Place',1,0),op('Place',22,1),op('Place',10,2),op('Cur',1),op('Infect',22,1),phase(4,0)]),
                skill(22,10),
                read('Your private eye is on number 10. Now cast your ordinary ballot before passing the turn.',
                    'Con mắt bí mật của bạn đã nằm trên lá số 10. Giờ bỏ phiếu thường rồi chuyển lượt nhé.'),
                vote(1,10,2),end(),
                read('It is Village’s voting turn now. Snooper looks ordinary; the selected eye is hidden from this side.',
                    'Đã tới lượt Village bỏ phiếu. Snooper trông như bình thường; con mắt đã chọn được giữ kín ở phía này.'),
                vote(10,1,0),tally(),
                read('The chosen target became public at resolution. You have seen healthy Snooper and its Decay from real turns on the same table.',
                    'Khi phân giải, mục tiêu đã chọn mới công khai. Bạn vừa thấy Snooper khỏe và Decay qua lượt chơi thật trên cùng một bàn.')]
        elif id=='card-gambler':
            # The opponent's actions are fixed; the learner only advances their completed turn.
            item['setup'][-1]=phase(4,0)
            result=[]
            for step in steps:
                if A[step['action']]=='View':
                    if step['seat']==1:result.append(end())
                    continue
                if step['prepare']:
                    for cmd in step['prepare']:
                        if cmd['kind']==ctx['O'].index('Phase'):cmd['value']=4;cmd['target']=0
                if 'You are Village, voting second.' in step['purpose']['english']:
                    step['purpose']=c('Cursermancer has already bet on number 10 in this prepared example. Look at their completed move; you will choose for Village next.',
                        'Trong tình huống đặt sẵn này, Cursermancer đã cược vào lá số 10. Nhìn nước họ vừa làm; lát nữa bạn sẽ quyết định cho Village.')
                if 'Another prepared situation' in step['purpose']['english']:
                    step['purpose']=c('Another prepared situation: Cursermancer has voted without betting. Look at their completed turn.',
                        'Tình huống mới: Cursermancer đã bỏ phiếu và không cược. Mình nhìn lượt họ vừa chơi nhé.')
                result.append(step)
            item['beats']=result
        elif id=='card-witch':
            result=[]
            for step in steps:
                if A[step['action']]=='View':result.append(end());continue
                if 'Numbers 1 and 10 are infected' in step['purpose']['english']:
                    step['purpose']=c('It is Cursermancer’s turn. Numbers 1 and 10 are gray on the table; number 11 is gray in hand. Gray means infected.',
                        'Đến lượt Cursermancer. Lá số 1 và số 10 trên bàn, lá số 11 trong tay đã chuyển xám. Màu xám cho biết lá đang bị nhiễm bệnh.');step['source']=11
                if 'Number 11 still has' in step['purpose']['english']:step['source']=11
                result.append(step)
            item['beats']=result
        elif id=='card-blind':
            item['beats']=[steps[0],cemetery(),
                read('Blind has inspected this death. Read the result from Village.',
                    'Blind vừa kiểm tra lần chết này. Nhìn kết quả từ Village nhé.'),cemetery(False),end(),cemetery(),
                read('It is Cursermancer’s turn. The truth matches Blind’s healthy report.',
                    'Đến lượt Cursermancer. Sự thật đang khớp với lời báo lúc Blind khỏe.'),cemetery(False),
                read('Blind and number 11 have turned gray: both are infected. Watch the next death.',
                    'Blind và lá số 11 đã chuyển xám: cả hai bị nhiễm bệnh. Mình quan sát lần chết tiếp theo nhé.',
                    [op('Infect',19,1),op('ResetSkill',19),op('Place',11,2),op('Infect',11,1)]),
                read('Blind has inspected the new death.','Blind vừa kiểm tra lần chết mới.',[op('Kill',11)]),
                cemetery(),read('Cursermancer sees the truth: number 11 was infected.',
                    'Cursermancer thấy sự thật: lá số 11 đã mang bệnh.'),cemetery(False),end(),cemetery(),
                read('It is Village’s turn. The same corpse looks ordinary in Blind’s report.',
                    'Đến lượt Village. Vẫn lá đã chết ấy, nhưng lời báo của Blind cho thấy một lá bình thường.'),cemetery(False)]
        elif id=='strategy-purge-trace':
            result=[]
            for step in steps:
                if A[step['action']]=='View':
                    if step['prepare']:
                        step['prepare']+=[phase(1,0)]
                        result.append(read('It is Cursermancer’s turn in this replay. Number 10 is gray in hand: infected.',
                            'Đến lượt Cursermancer trong tình huống phát lại. Lá số 10 trong tay đã xám: nó bị nhiễm bệnh.',step['prepare'],10))
                    else:result.append(end())
                    continue
                if 'infected number 10' in step['purpose']['english']:step['source']=10
                result.append(step)
            item['beats']=result
        assert all(A[s['action']]!='View' for s in item['beats']),id
    return lessons

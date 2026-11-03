"""Update only academy-owned rows and add explicit editable HUD/view controls.

Does not touch CardUI prefab, approved icons, Blind or any card skill source.
"""
from author_tutorial_academy import *
ensure_meta(ROOT/'Assets/Scripts/PlayerStageHUD.cs');SCRIPTS['PlayerStageHUD']=guid('Assets/Scripts/PlayerStageHUD.cs')

# Rebind the existing owned row objects after combining the lessons.
s=Scene('Assets/Scenes/MenuScenes.unity',True)
academy=s.find_script(guid('Assets/Scripts/TutorialAcademyUI.cs'))
kept=[]
pages=[int(x) for x in re.findall(r'fileID: (\d+)',re.search(r'(?ms)^  lessonPages:.*?(?=^  \w+:|\Z)',s.records[academy])[0])]
for chapter,page in enumerate(pages):
    pgtr=s.component(page,type_=224)
    ctr=next(t for t in s.children(pgtr) if s.value(s.game_object(t),'m_Name').strip('"')=='Editable lesson rows')
    rows=sorted(s.children(ctr),key=lambda t:-float(re.search(r'y: (-?[\d.]+)',s.value(t,'m_AnchoredPosition'))[1]))
    group=[l for l in lessons if l['chapter']==chapter]
    assert len(rows)>=len(group)
    s.position(ctr,(0,0,1412,max(590,len(group)*124)))
    for index,tr in enumerate(rows):
        go=s.game_object(tr);lc=s.component(go,script=guid('Assets/Scripts/TutorialLessonTile.cs'))
        s.set(go,'m_IsActive',1 if index<len(group) else 0)
        if index>=len(group):continue
        exercise=group[index]
        s.set(lc,'exercise',ext('Assets/UI/Tutorial/Lessons/'+exercise['id']+'.asset'));kept.append(lc)
        s.set(go,'m_Name',scalar('Lesson - '+exercise['id']))
    # No empty scroll gap after merging the controls and energy pages.
    scroll=s.component(page,script=SCRIPTS['ScrollRect'])
    s.set(scroll,'m_Vertical',1 if len(group)>4 else 0)
s.records[academy]=re.sub(r'(?ms)^  lessonTiles:.*?(?=^  \w+:|\Z)',lambda _:array('lessonTiles',kept),s.records[academy])
save(s)

for scene in ['MainBoard','TutorialBoard']:
    s=Scene('Assets/Scenes/'+scene+'.unity',True)
    view=s.find_script(guid('Assets/Scripts/GameStartUI.cs'));safe=get(s,view,'safeFrame')
    tm=get(s,view,'turns')
    if s.find_script(SCRIPTS['PlayerStageHUD']) is None:
        go,tr=s.node('Your faction and current stage - always visible',safe,(332,108,1244,36))
        s.canvas(go,450)
        comp=s.mono(go,'PlayerStageHUD')
        badge,btr=s.node('View faction badge',tr,(0,0,400,36));im=s.image(badge,tint=(.14,.31,.26,.75))
        faction,_=label(s,'Your faction',btr,(12,4,376,28),'YOUR FACTION: Village',18)
        stage,_=label(s,'Current public stage',tr,(420,4,824,28),'ACTION TURN · Village',18)
        for k,v in dict(turns=ref(tm),factionLabel=ref(faction),stageLabel=ref(stage),factionTint=ref(im)).items():s.set(comp,k,v)
    hud=s.find_script(SCRIPTS['PlayerStageHUD']);image=get(s,hud,'factionTint')
    s.set(image,'m_Sprite',sprite('painted-panel'));s.set(image,'m_Type',1)
    s.set(image,'m_PixelsPerUnitMultiplier',10);s.set(image,'m_Color','{r: 1, g: 1, b: 1, a: 1}')
    caption=get(s,hud,'factionLabel');s.records[caption]=re.sub(r'(m_Alignment:) \d+',r'\g<1> 4',s.records[caption])
    s.position(s.component(s.game_object(hud),type_=224),(332,108,1244,68))
    s.position(s.component(s.game_object(image),type_=224),(0,0,1244,64))
    s.position(s.component(s.game_object(caption),type_=224),(22,12,376,40))
    stage=get(s,hud,'stageLabel');s.position(s.component(s.game_object(stage),type_=224),(420,12,798,40))
    s.records[stage]=re.sub(r'(m_Alignment:) \d+',r'\g<1> 4',s.records[stage])
    # Preserve the modal content/grid; reserve a distinct rectangle for the lesson sidebar.
    note=get(s,tm,'notePanel');noteRect=s.component(note,type_=224)
    s.parent(noteRect,safe);s.position(noteRect,(332,184,1244,692))
    for child in s.children(noteRect):
        if s.value(s.game_object(child),'m_Name').strip('"')=='NoteWindow':
            s.position(child,(0,0,1260,780),center=True,scale=.86)
    # Expose all sixteen tokens and remove the duplicate NoteBlock on token 10.
    seen=set()
    for comp in list(s.records):
        if s.types[comp]!=114 or guid('Assets/Scripts/NoteBlock.cs') not in s.records[comp]:continue
        go=s.game_object(comp)
        if go in seen:
            ids=[int(x) for x in re.findall(r'component: \{fileID: (-?\d+)\}',s.records[go])]
            s.components(go,[x for x in ids if x!=comp]);s.records.pop(comp);s.types.pop(comp);continue
        seen.add(go);s.set(go,'m_IsActive',1)
    cem=s.find_script(guid('Assets/Scripts/CemeteryView.cs'));cemgo=s.game_object(cem);cemtr=s.component(cemgo,type_=224)
    s.parent(cemtr,safe);s.position(cemtr,(332,208,1244,652))
    # Refit the title and Close button to the modal width.
    for tr in s.children(cemtr):
        name=s.value(s.game_object(tr),'m_Name').strip('"')
        if 'Title' in name or 'Heading' in name:s.position(tr,(28,18,920,50))
        if 'Close' in name:s.position(tr,(1000,20,210,42))
        if name=='Cemetery Cards - centered grid':s.position(tr,(32,78,1180,542))
        if name=='Cemetery Empty':s.position(tr,(160,300,900,48))
    if scene=='TutorialBoard':
        work=s.find_script(guid('Assets/Scripts/TutorialWorkshop.cs'));overlay=get(s,work,'overlay')
        # Discard old answer controls from the actual UI (fields remain for old serialized scenes).
        s.set(get(s,work,'quizPanel'),'m_IsActive',0)
        def remove_node(go):
            tr=s.component(go,type_=224)
            for child in list(s.children(tr)):remove_node(s.game_object(child))
            s.parent(tr,0)
            ids=[int(x) for x in re.findall(r'component: \{fileID: (-?\d+)\}',s.records[go])]
            for x in ids+[go]:
                s.records.pop(x,None);s.types.pop(x,None)
        obsolete=[i for i,body in s.records.items() if s.types[i]==1 and
            any(s.value(i,'m_Name').strip('"').startswith(n) for n in
                ['Workshop Cursermancer viewpoint','Workshop Village viewpoint','Cursermancer-only infection hint'])]
        for go in obsolete:
            if go in s.records:remove_node(go)
        for field in ['curViewButton','villageViewButton','curViewLabel','villageViewLabel','infectionHints','infectionLabels']:
            s.records[work]=re.sub(r'(?ms)^  '+field+r':.*?(?=^  \w+:|\Z)','',s.records[work])
        if '  passWagerButton:' not in s.records[work]:
            first=get(s,work,'continueButton');rect=s.component(s.game_object(first),type_=224)
            parent=get(s,rect,'m_Father')
            go,_,control,caption=button(s,'Workshop Pass Gambler wager',parent,(20,584,244,48),'Pass · do not bet',work,'PassWager','TutorialWorkshop')
            s.set(go,'m_IsActive',0);s.set(work,'passWagerButton',ref(control));s.set(work,'passWagerLabel',ref(caption))
    save(s)
print('Updated 22 action-first lessons, natural turn changes and saved faction/stage HUD.')

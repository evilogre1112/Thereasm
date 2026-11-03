"""Setup tutorial academy lesson assets and UI components."""
import json,re,shutil,uuid
from migrate_scene_ui import *
from tutorial_curriculum_data import lessons,chapters
def save(s):
    # Scene.save writes roots last and removes that record from its working map.
    # Keep it available while adding more independent authored controls below.
    roots=s.records[9223372036854775807];s.save();s.records[9223372036854775807]=roots
for name in ['TutorialExercise','TutorialCourse','TutorialAcademyUI','TutorialChapterTile','TutorialLessonTile','TutorialWorkshop']:
    ensure_meta(ROOT/f'Assets/Scripts/{name}.cs');SCRIPTS[name]=guid(f'Assets/Scripts/{name}.cs')
for folder in ['Assets/UI/Tutorial/Icons','Assets/UI/Tutorial/Lessons']:
    (ROOT/folder).mkdir(parents=True,exist_ok=True);ensure_meta(ROOT/folder)
for name in ['MenuScenes','TutorialBoard']:
    rel=f'Assets/Scenes/{name}.unity';backup=ROOT/'Backups/2026-11-02-tutorial-academy'/rel
    if not backup.exists():backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,backup)
template=(ROOT/'Assets/UI/ModeIcons/mode-village.png.meta').read_text()
for name in ['controls','energy','cards','strategy']:
    p=ROOT/f'Assets/UI/Tutorial/Icons/{name}.png';m=p.with_name(p.name+'.meta')
    if not m.exists():
        data=re.sub(r'^guid: \w+',lambda _:'guid: '+uuid.uuid4().hex,template,count=1,flags=re.M)
        data=re.sub(r'(maxTextureSize:) \d+',r'\g<1> 512',data);m.write_text(data,encoding='utf-8')
def icon(name):return f'{{fileID: 21300000, guid: {guid(f"Assets/UI/Tutorial/Icons/{name}.png")}, type: 3}}'
class Raw(str):pass
def atom(v):return str(v) if isinstance(v,Raw) else '[]' if isinstance(v,list) else '{}' if isinstance(v,dict) else json.dumps(v,ensure_ascii=False)
def dump(data,n=0):
    pad=' '*n
    if isinstance(data,dict):return ''.join(pad+k+(':\n'+dump(v,n+2) if isinstance(v,(dict,list)) and v else ': '+atom(v)+'\n') for k,v in data.items())
    result=''
    for v in data:
        if isinstance(v,dict):
            lines=dump(v,n+2).splitlines();result+=pad+'- '+lines[0].lstrip()+'\n'+'\n'.join(lines[1:])+'\n'
        else:result+=pad+'- '+atom(v)+'\n'
    return result
def ext(path,fid=11400000,ty=2):return Raw(f'{{fileID: {fid}, guid: {guid(path)}, type: {ty}}}')
def asset(path,script,name,fields):
    p=ROOT/path;ensure_meta(p)
    common=dict(m_ObjectHideFlags=0,m_CorrespondingSourceObject=Raw('{fileID: 0}'),m_PrefabInstance=Raw('{fileID: 0}'),m_PrefabAsset=Raw('{fileID: 0}'),m_GameObject=Raw('{fileID: 0}'),m_Enabled=1,m_EditorHideFlags=0,m_Script=ext(f'Assets/Scripts/{script}.cs',11500000,3),m_Name=name,m_EditorClassIdentifier='')
    p.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'+dump(dict(common,**fields),2),encoding='utf-8')
for lesson in lessons:
    fields={k:v for k,v in lesson.items() if k!='chapter'};fields['cards']=[ext(f'Assets/ScriptableObjects/{a}.asset') for a in fields['cards']]
    asset(f'Assets/UI/Tutorial/Lessons/{lesson["id"]}.asset','TutorialExercise',lesson['id'],fields)
coursePath='Assets/UI/Tutorial/TutorialCourse.asset'
asset(coursePath,'TutorialCourse','VillageFieldGuide',dict(chapters=[dict(ch,icon=Raw(icon(ch['id'])),lessons=[ext(f'Assets/UI/Tutorial/Lessons/{l["id"]}.asset') for l in lessons if l['chapter']==i]) for i,ch in enumerate(chapters)]))
readable=re.search(r'^  readableFont: (.*)',(ROOT/'Assets/UI/Localization/GameLocalization.asset').read_text(encoding='utf-8-sig'),re.M)[1]
def label(s,name,parent,rect,text,size=20,center=False):
    c,tr=s.label(name,parent,rect,text,size,center=center);s.records[c]=re.sub(r'(?m)^    m_Font:.*$',f'    m_Font: {readable}',s.records[c]);return c,tr
def button(s,name,parent,rect,text,target,method,cls):
    go,tr,b,c=s.button(name,parent,rect,text,target,method,cls);s.records[c]=re.sub(r'(?m)^    m_Font:.*$',f'    m_Font: {readable}',s.records[c]);return go,tr,b,c
def get(s,c,key):return int(re.search(r'fileID: (-?\d+)',s.value(c,key))[1])
def children(s,tr):
    out=[]
    for c in s.children(tr):out+=[c]+children(s,c)
    return out
def raycaster(s,go):s.mono(go,'GraphicRaycaster','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
s=Scene('Assets/Scenes/MenuScenes.unity',True)
if s.find_script(SCRIPTS['TutorialAcademyUI']) is None:
    view=s.find_script(guid('Assets/Scripts/GameStartUI.cs'));menu=get(s,view,'menuController');safe=get(s,view,'safeFrame')
    go,tr=s.node('Tutorial Academy - saved bilingual panels',safe,(0,0,1600,900));s.canvas(go,1100);raycaster(s,go);controller=s.mono(go,'TutorialAcademyUI')
    welcome,wtr=s.node('First launch question - once per installation',tr,(0,0,1600,900),False);s.image(welcome,tint=(.025,.007,.009,.9),raycast=True)
    box,btr=s.panel('First night welcome',wtr,(280,190,1040,510))
    wi,witr=s.node('Welcome emblem',btr,(34,90,180,180));im=s.image(wi,icon('controls'));s.set(im,'m_PreserveAspect',1)
    wt,_=label(s,'Welcome title',btr,(240,100,740,65),'Your first night in the village?',32)
    wb,_=label(s,'Welcome invitation',btr,(240,180,730,150),'A few short lessons will help you find your feet.',23)
    _,_,_,nt=button(s,'New player choice',btr,(44,375,462,70),"I'm new · Show me around",controller,'NewPlayer','TutorialAcademyUI')
    _,_,_,ot=button(s,'Returning player choice',btr,(534,375,462,70),"I've played before · Open menu",controller,'ReturningPlayer','TutorialAcademyUI')
    language=s.find_script(guid('Assets/Scripts/LanguageController.cs'))
    button(s,'Welcome English',btr,(670,30,130,44),'English',language,'English','LanguageController');button(s,'Welcome Vietnamese',btr,(820,30,170,44),'Tiếng Việt',language,'Vietnamese','LanguageController')
    academy,atr=s.node('Tutorial subject picker',tr,(0,0,1600,900),False);s.image(academy,tint=(.025,.007,.009,.94),raycast=True)
    panel,ptr=s.panel('Field guide panel',atr,(45,35,1510,830))
    ht,_=label(s,'Academy title',ptr,(36,32,1120,58),'THE VILLAGE FIELD GUIDE',32);hint,_=label(s,'Academy invitation',ptr,(36,100,1410,64),'Choose a subject. Each lesson is a small, playable situation.',21)
    _,_,_,bt=button(s,'Academy Back',ptr,(1210,32,258,52),'Back to menu',controller,'Back','TutorialAcademyUI')
    grid,gtr=s.node('Four tutorial subjects',ptr,(36,186,1430,584));tiles=[];pages=[];rows=[]
    for i,ch in enumerate(chapters):
        tile,ttr=s.panel('Subject - '+ch['id'],gtr,((i%2)*728,(i//2)*286,702,264));tc=s.mono(tile,'TutorialChapterTile')
        button(s,'Open subject '+ch['id'],ttr,(0,0,702,264),'',tc,'Open','TutorialChapterTile')
        art,arttr=s.node('Subject asset - '+ch['id'],ttr,(22,60,150,150));im=s.image(art,icon(ch['id']));s.set(im,'m_PreserveAspect',1)
        title,_=label(s,'Subject title',ttr,(188,30,470,66),ch['title']['english'],28);desc,_=label(s,'Subject invitation',ttr,(188,110,470,86),ch['summary']['english'],20);prog,_=label(s,'Subject completion',ttr,(188,218,470,28),'0 lessons completed',18)
        for k,v in dict(academy=ref(controller),chapter=i,title=ref(title),summary=ref(desc),progress=ref(prog),icon=ref(im)).items():s.set(tc,k,v)
        tiles.append(tc)
        pg,pgtr=s.node('Lessons - '+ch['id'],ptr,(36,178,1430,590),False);s.image(pg,tint=(0,0,0,0),raycast=True)
        s.mono(pg,'RectMask2D','  m_Padding: {x: 0, y: 0, z: 0, w: 0}\n  m_Softness: {x: 0, y: 0}\n')
        group=[l for l in lessons if l['chapter']==i];content,ctr=s.node('Editable lesson rows',pgtr,(0,0,1412,len(group)*124))
        for row,l in enumerate(group):
            rg,rtr=s.panel('Lesson - '+l['id'],ctr,(0,row*124,1408,112));lc=s.mono(rg,'TutorialLessonTile');button(s,'Play lesson '+l['id'],rtr,(0,0,1408,112),'',lc,'Play','TutorialLessonTile')
            ag,agtr=s.node('Chapter symbol',rtr,(18,20,72,72));im=s.image(ag,icon(ch['id']));s.set(im,'m_PreserveAspect',1)
            rt,_=label(s,'Lesson title',rtr,(110,16,1015,35),l['title']['english'],24);rd,_=label(s,'Lesson promise',rtr,(110,57,1015,46),l['summary']['english'],18);rs,_=label(s,'Lesson status',rtr,(1155,30,220,48),'Play lesson',19,True)
            for k,v in dict(academy=ref(controller),exercise=ext(f'Assets/UI/Tutorial/Lessons/{l["id"]}.asset'),title=ref(rt),summary=ref(rd),status=ref(rs)).items():s.set(lc,k,v)
            rows.append(lc)
        s.mono(pg,'ScrollRect',f'  m_Content: {ref(ctr)}\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 45\n  m_Viewport: {ref(pgtr)}\n  m_HorizontalScrollbar: {{fileID: 0}}\n  m_VerticalScrollbar: {{fileID: 0}}\n  m_HorizontalScrollbarVisibility: 0\n  m_VerticalScrollbarVisibility: 0\n  m_HorizontalScrollbarSpacing: 0\n  m_VerticalScrollbarSpacing: 0\n  m_OnValueChanged:\n    m_PersistentCalls:\n      m_Calls: []\n');pages.append(pg)
    fields=dict(menu=ref(menu),course=ext(coursePath),welcomePanel=ref(welcome),academyPanel=ref(academy),chapterGrid=ref(grid),welcomeTitle=ref(wt),welcomeBody=ref(wb),newLabel=ref(nt),returningLabel=ref(ot),academyTitle=ref(ht),academyHint=ref(hint),backLabel=ref(bt))
    for k,v in fields.items():s.set(controller,k,v)
    for k,v in [('lessonPages',pages),('chapterTiles',tiles),('lessonTiles',rows)]:s.records[controller]+=array(k,v)
    save(s)
s=Scene('Assets/Scenes/TutorialBoard.unity',True)
if s.find_script(SCRIPTS['TutorialWorkshop']) is None:
    g=s.find_script(guid('Assets/Scripts/TutorialController.cs'));work=s.mono(s.game_object(g),'TutorialWorkshop');s.set(g,'workshop',ref(work))
    for field in ['turns','deck','overlay','sourceFrame','targetFrame','directionArrow','spotlight','progress','title','instruction','purpose','feedback','continueLabel','header','headerHint','continueButton','progressFill','startControl','endControl','checkControl','cemeteryControl','setupPanel','resultPanel']:s.set(work,field,s.value(g,field))
    s.set(work,'guide',ref(g));s.set(work,'detailsPanel',s.value(g,'heartGuidePanel'));s.set(work,'detailsText',s.value(g,'heartGuideText'));choice=get(s,g,'practiceButton');s.set(work,'choiceButton',ref(choice))
    texts=[s.component(s.game_object(tr),script=SCRIPTS['Text']) for tr in children(s,s.component(s.game_object(choice),type_=224))];s.set(work,'choiceLabel',ref(next(t for t in texts if t is not None)))
    notes=next(i for i,b in s.records.items() if s.types[i]==114 and 'm_MethodName: ToggleNote' in b);s.set(work,'notesButton',ref(notes))
    controlled=re.search(r'  controlledButtons:.*?(?=^  \w+:|\Z)',s.records[g],re.M|re.S)
    if controlled:s.records[work]+=controlled[0]
    s.records[work]+='  noteSlots: []\n';save(s)
if s.find_script(SCRIPTS['TutorialWorkshop']) is not None:
    work=s.find_script(SCRIPTS['TutorialWorkshop']);g=s.find_script(guid('Assets/Scripts/TutorialController.cs'))
    if '  quizPanel:' not in s.records[work]:
        overlay=get(s,g,'overlay');qg,qtr=s.panel('Workshop decision - two clear answers',overlay,(360,465,1150,220),False)
        s.canvas(qg,420);raycaster(s,qg)
        question,_=label(s,'Workshop decision question',qtr,(30,25,1090,66),'Choose the explanation that fits.',24,True)
        _,_,first,firstLabel=button(s,'Workshop answer A',qtr,(30,120,530,70),'Answer A',work,'AnswerFirst','TutorialWorkshop')
        _,_,second,secondLabel=button(s,'Workshop answer B',qtr,(590,120,530,70),'Answer B',work,'AnswerSecond','TutorialWorkshop')
        for k,v in dict(quizPanel=ref(qg),quizQuestion=ref(question),quizFirstButton=ref(first),quizFirstLabel=ref(firstLabel),choiceButton=ref(second),choiceLabel=ref(secondLabel),legacyPractice=ref(s.game_object(get(s,g,'practiceButton')))).items():s.set(work,k,v)
        headings=[i for i,b in s.records.items() if s.types[i]==1 and any('m_Name: '+scalar(n) in b for n in ['Tutorial Action Heading','Tutorial Purpose Heading'])]
        s.records[work]+=array('sectionHeadings',headings);save(s)
work=s.find_script(SCRIPTS['TutorialWorkshop']);g=s.find_script(guid('Assets/Scripts/TutorialController.cs'))
if work is not None and not any('m_Name: "Workshop Notes"' in b for b in s.records.values()):
    first=get(s,g,'continueButton');firstRect=s.component(s.game_object(first),type_=224);parent=int(re.search(r'fileID: (\d+)',s.value(firstRect,'m_Father'))[1])
    notesGo,_,notes,_=button(s,'Workshop Notes',parent,(20,584,244,48),'Notes',work,'ToggleNotes','TutorialWorkshop');s.set(notesGo,'m_IsActive',0);s.set(work,'notesButton',ref(notes));save(s)
if work is not None and '  previewExercise:' not in s.records[work]:
    s.set(work,'previewExercise',ext('Assets/UI/Tutorial/Lessons/controls-first-night.asset'));save(s)
if work is not None:
    slots=[]
    for i,b in s.records.items():
        if guid('Assets/Scripts/NoteSlot.cs') in b and s.types[i]==114:
            name=s.value(s.game_object(i),'m_Name').strip('"')
            if name.startswith('Note Seat '):slots.append((int(name.rsplit(' ',1)[1]),i))
    assert len(slots)==7
    block=array('noteSlots',[i for _,i in sorted(slots)])
    s.records[work]=re.sub(r'(?ms)^  noteSlots:.*?(?=^  \w+:|\Z)',lambda _:block,s.records[work])
    for i,b in s.records.items():
        if s.types[i]==1 and 'm_Name: "Workshop Notes"' in b:s.set(i,'m_IsActive',0)
    save(s)
print(f'Authored {len(lessons)} lessons, four subject pages, first-launch question and saved workshop references.')

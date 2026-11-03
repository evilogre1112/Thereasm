import re
import shutil
from pathlib import Path

ROOT = Path(r"d:\Project\Thereasm")

IMG_PANEL_GUID = "36d7020e9ba84117b26a83c7a79d9daa"   # painted-panel.png
IMG_ICON_GUID = "4c9a8101e45b46e3995f700000000001"    # note-icon.png
IMG_TOKEN_GUID = "4c9a8101e45b46e3995f700000000002"   # note-token.png
IMG_SLOT_GUID = "4c9a8101e45b46e3995f700000000003"    # note-slot.png
IMG_DIVIDER_GUID = "4c9a8101e45b46e3995f700000000004" # note-divider.png
IMG_CLOSE_GUID = "4c9a8101e45b46e3995f700000000005"   # note-close-icon.png

TMP_FONT_GUID = "c36ad35b43da7964d903dd4acb8deac3"    # Almendra-Regular SDF
TMP_MAT_ID = "3542882934977196910"
BUTTON_GUID = "4e29b1a8efbd4b44bb3f3716e73f07ff"
IMAGE_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"
TMP_GUID = "f4688fdb7df04437aeb418b961361dc5"

def build_yaml_blocks():
    # Notes button icon (fileIDs 7000000000000000501–504).
    btn_icon = """--- !u!1 &7000000000000000501
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000502}
  - component: {fileID: 7000000000000000503}
  - component: {fileID: 7000000000000000504}
  m_Layer: 5
  m_Name: "Notes Button Icon"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000502
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000501}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000051}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0.5}
  m_AnchorMax: {x: 0, y: 0.5}
  m_AnchoredPosition: {x: 26, y: 0}
  m_SizeDelta: {x: 32, y: 32}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000503
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000501}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000504
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000501}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000001, type: 3}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

    # Notebook window (fileIDs 7000000000000000510–513).
    note_window = """--- !u!1 &7000000000000000510
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000511}
  - component: {fileID: 7000000000000000512}
  - component: {fileID: 7000000000000000513}
  m_Layer: 5
  m_Name: "NoteWindow"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000511
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000510}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 7000000000000000521}
  - {fileID: 7000000000000000531}
  - {fileID: 7000000000000000541}
  - {fileID: 7000000000000000551}
  - {fileID: 7000000000000000561}
  - {fileID: 7000000000000000571}
  - {fileID: 1583333837}
  - {fileID: 7000000000000000581}
  - {fileID: 7000000000000000591}
  - {fileID: 1537557985}
  - {fileID: 7000000000000000601}
  m_Father: {fileID: 1927024419}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 1260, y: 780}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000512
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000510}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000513
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000510}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 36d7020e9ba84117b26a83c7a79d9daa, type: 3}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 8
"""

    # Notebook header icon (fileIDs 7000000000000000520–523).
    header_icon = """--- !u!1 &7000000000000000520
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000521}
  - component: {fileID: 7000000000000000522}
  - component: {fileID: 7000000000000000523}
  m_Layer: 5
  m_Name: "Note Header Icon"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000521
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000520}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: -360, y: 312}
  m_SizeDelta: {x: 64, y: 64}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000522
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000520}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000523
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000520}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000001, type: 3}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

    # Notebook title (fileIDs 7000000000000000530–533).
    note_title = """--- !u!1 &7000000000000000530
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000531}
  - component: {fileID: 7000000000000000532}
  - component: {fileID: 7000000000000000533}
  m_Layer: 5
  m_Name: "Note Title"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000531
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000530}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 326}
  m_SizeDelta: {x: 640, y: 38}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000532
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000530}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000533
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000530}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: "S\u1ED4 TAY \u0110I\u1EC0U TRA"
  m_isRightToLeft: 0
  m_fontAsset: {fileID: 11400000, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_sharedMaterial: {fileID: 3542882934977196910, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_fontSharedMaterials: []
  m_fontMaterial: {fileID: 0}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4291880439
  m_fontColor: {r: 0.968627, g: 0.831373, b: 0.6, a: 1}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {r: 1, g: 1, b: 1, a: 1}
    topRight: {r: 1, g: 1, b: 1, a: 1}
    bottomLeft: {r: 1, g: 1, b: 1, a: 1}
    bottomRight: {r: 1, g: 1, b: 1, a: 1}
  m_fontColorGradientPreset: {fileID: 0}
  m_spriteAsset: {fileID: 0}
  m_tintAllSprites: 0
  m_StyleSheet: {fileID: 0}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 32
  m_fontSizeBase: 32
  m_fontWeight: 700
  m_enableAutoSizing: 0
  m_fontSizeMin: 18
  m_fontSizeMax: 72
  m_fontStyle: 1
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 2
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 0
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {fileID: 0}
  parentLinkedComponent: {fileID: 0}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {x: 0, y: 0, z: 0, w: 0}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {fileID: 0}
  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}
"""

    # Notebook subtitle (fileIDs 7000000000000000540–543).
    note_subtitle = """--- !u!1 &7000000000000000540
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000541}
  - component: {fileID: 7000000000000000542}
  - component: {fileID: 7000000000000000543}
  m_Layer: 5
  m_Name: "Note Subtitle"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000541
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000540}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 292}
  m_SizeDelta: {x: 750, y: 24}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000542
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000540}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000543
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000540}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: "K\u00E9o th\u1EA3 th\u1EBB s\u1ED1 ng\u01B0\u1EDDi ch\u01A1i \u0111\u1EC3 suy lu\u1EADn v\u00E0 theo d\u00F5i c\u00E1c gh\u1EBF nghi v\u1EA5n"
  m_isRightToLeft: 0
  m_fontAsset: {fileID: 11400000, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_sharedMaterial: {fileID: 3542882934977196910, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_fontSharedMaterials: []
  m_fontMaterial: {fileID: 0}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 3670985976
  m_fontColor: {r: 0.972549, g: 0.92549, b: 0.835294, a: 0.85}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {r: 1, g: 1, b: 1, a: 1}
    topRight: {r: 1, g: 1, b: 1, a: 1}
    bottomLeft: {r: 1, g: 1, b: 1, a: 1}
    bottomRight: {r: 1, g: 1, b: 1, a: 1}
  m_fontColorGradientPreset: {fileID: 0}
  m_spriteAsset: {fileID: 0}
  m_tintAllSprites: 0
  m_StyleSheet: {fileID: 0}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 15
  m_fontSizeBase: 15
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 12
  m_fontSizeMax: 30
  m_fontStyle: 0
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 0
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {fileID: 0}
  parentLinkedComponent: {fileID: 0}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {x: 0, y: 0, z: 0, w: 0}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {fileID: 0}
  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}
"""

    # Close button (fileIDs 7000000000000000550–554).
    close_btn = """--- !u!1 &7000000000000000550
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000551}
  - component: {fileID: 7000000000000000552}
  - component: {fileID: 7000000000000000553}
  - component: {fileID: 7000000000000000554}
  m_Layer: 5
  m_Name: "Close Note Button"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000551
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000550}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 540, y: 312}
  m_SizeDelta: {x: 44, y: 44}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000552
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000550}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000553
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000550}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000005, type: 3}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
--- !u!114 &7000000000000000554
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000550}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {fileID: 0}
    m_SelectOnDown: {fileID: 0}
    m_SelectOnLeft: {fileID: 0}
    m_SelectOnRight: {fileID: 0}
  m_Transition: 1
  m_Colors:
    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}
    m_HighlightedColor: {r: 1, g: 0.86, b: 0.64, a: 1}
    m_PressedColor: {r: 0.7, g: 0.56, b: 0.42, a: 1}
    m_SelectedColor: {r: 1, g: 0.86, b: 0.64, a: 1}
    m_DisabledColor: {r: 0.55, g: 0.5, b: 0.47, a: 0.65}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1
  m_SpriteState:
    m_HighlightedSprite: {fileID: 0}
    m_PressedSprite: {fileID: 0}
    m_SelectedSprite: {fileID: 0}
    m_DisabledSprite: {fileID: 0}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: 1
  m_TargetGraphic: {fileID: 7000000000000000553}
  m_OnClick:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {fileID: 2042111136}
        m_TargetAssemblyTypeName: TurnManager, Assembly-CSharp
        m_MethodName: ToggleNote
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {fileID: 0}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument: 
          m_BoolArgument: 0
        m_CallState: 2
"""

    # Upper divider (fileIDs 7000000000000000560–563).
    div_top = """--- !u!1 &7000000000000000560
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000561}
  - component: {fileID: 7000000000000000562}
  - component: {fileID: 7000000000000000563}
  m_Layer: 5
  m_Name: "Divider Top"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000561
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000560}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 254}
  m_SizeDelta: {x: 1060, y: 18}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000562
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000560}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000563
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000560}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000004, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

    # Suspect-row label (fileIDs 7000000000000000570–573).
    suspect_lbl = """--- !u!1 &7000000000000000570
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000571}
  - component: {fileID: 7000000000000000572}
  - component: {fileID: 7000000000000000573}
  m_Layer: 5
  m_Name: "Suspects Section Label"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000571
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000570}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 222}
  m_SizeDelta: {x: 800, y: 26}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000572
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000570}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000573
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000570}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: "\u2726 H\u00C0NG GH\u1EBE NGHI V\u1EA4N (1 - 6) \u2726"
  m_isRightToLeft: 0
  m_fontAsset: {fileID: 11400000, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_sharedMaterial: {fileID: 3542882934977196910, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_fontSharedMaterials: []
  m_fontMaterial: {fileID: 0}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4291880439
  m_fontColor: {r: 0.968627, g: 0.831373, b: 0.6, a: 1}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {r: 1, g: 1, b: 1, a: 1}
    topRight: {r: 1, g: 1, b: 1, a: 1}
    bottomLeft: {r: 1, g: 1, b: 1, a: 1}
    bottomRight: {r: 1, g: 1, b: 1, a: 1}
  m_fontColorGradientPreset: {fileID: 0}
  m_spriteAsset: {fileID: 0}
  m_tintAllSprites: 0
  m_StyleSheet: {fileID: 0}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 17
  m_fontSizeBase: 17
  m_fontWeight: 700
  m_enableAutoSizing: 0
  m_fontSizeMin: 12
  m_fontSizeMax: 30
  m_fontStyle: 1
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 1
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 0
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {fileID: 0}
  parentLinkedComponent: {fileID: 0}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {x: 0, y: 0, z: 0, w: 0}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {fileID: 0}
  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}
"""

    # Lower divider (fileIDs 7000000000000000580–583).
    div_bot = """--- !u!1 &7000000000000000580
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000581}
  - component: {fileID: 7000000000000000582}
  - component: {fileID: 7000000000000000583}
  m_Layer: 5
  m_Name: "Divider Bottom"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000581
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000580}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 48}
  m_SizeDelta: {x: 1060, y: 18}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000582
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000580}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000583
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000580}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 21300000, guid: 4c9a8101e45b46e3995f700000000004, type: 3}
  m_Type: 0
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
"""

    # Token-pool label (fileIDs 7000000000000000590–593).
    pool_lbl = """--- !u!1 &7000000000000000590
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000591}
  - component: {fileID: 7000000000000000592}
  - component: {fileID: 7000000000000000593}
  m_Layer: 5
  m_Name: "Pool Section Label"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000591
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000590}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: 16}
  m_SizeDelta: {x: 800, y: 26}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000592
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000590}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000593
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000590}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: "\u2726 DANH S\u00C1CH NG\u01AF\u1EDCI CH\u01A0I (1 - 16) \u2726"
  m_isRightToLeft: 0
  m_fontAsset: {fileID: 11400000, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_sharedMaterial: {fileID: 3542882934977196910, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_fontSharedMaterials: []
  m_fontMaterial: {fileID: 0}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 4291880439
  m_fontColor: {r: 0.968627, g: 0.831373, b: 0.6, a: 1}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {r: 1, g: 1, b: 1, a: 1}
    topRight: {r: 1, g: 1, b: 1, a: 1}
    bottomLeft: {r: 1, g: 1, b: 1, a: 1}
    bottomRight: {r: 1, g: 1, b: 1, a: 1}
  m_fontColorGradientPreset: {fileID: 0}
  m_spriteAsset: {fileID: 0}
  m_tintAllSprites: 0
  m_StyleSheet: {fileID: 0}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 17
  m_fontSizeBase: 17
  m_fontWeight: 700
  m_enableAutoSizing: 0
  m_fontSizeMin: 12
  m_fontSizeMax: 30
  m_fontStyle: 1
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 1
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 0
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {fileID: 0}
  parentLinkedComponent: {fileID: 0}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {x: 0, y: 0, z: 0, w: 0}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {fileID: 0}
  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}
"""

    # Footer hint (fileIDs 7000000000000000600–603).
    bottom_hint = """--- !u!1 &7000000000000000600
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 7000000000000000601}
  - component: {fileID: 7000000000000000602}
  - component: {fileID: 7000000000000000603}
  m_Layer: 5
  m_Name: "Bottom Hint"
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &7000000000000000601
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000600}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 7000000000000000511}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0.5, y: 0.5}
  m_AnchorMax: {x: 0.5, y: 0.5}
  m_AnchoredPosition: {x: 0, y: -326}
  m_SizeDelta: {x: 950, y: 24}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!222 &7000000000000000602
CanvasRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000600}
  m_CullTransparentMesh: 1
--- !u!114 &7000000000000000603
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 7000000000000000600}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: f4688fdb7df04437aeb418b961361dc5, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_RaycastTarget: 0
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_text: "\u203B B\u1EA5m n\u00FAt \u2715 ho\u1EB7c b\u1EA5m ra ngo\u00E0i c\u1EEDa s\u1ED5 \u0111\u1EC3 \u0111\u00F3ng s\u1ED5 tay \u2022 V\u1ECB tr\u00ED c\u00E1c th\u1EBB s\u1ED1 s\u1EBD \u0111\u01B0\u1EE3c l\u01B0u l\u1EA1i"
  m_isRightToLeft: 0
  m_fontAsset: {fileID: 11400000, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_sharedMaterial: {fileID: 3542882934977196910, guid: c36ad35b43da7964d903dd4acb8deac3, type: 2}
  m_fontSharedMaterials: []
  m_fontMaterial: {fileID: 0}
  m_fontMaterials: []
  m_fontColor32:
    serializedVersion: 2
    rgba: 3236025556
  m_fontColor: {r: 0.83, g: 0.77, b: 0.63, a: 0.75}
  m_enableVertexGradient: 0
  m_colorMode: 3
  m_fontColorGradient:
    topLeft: {r: 1, g: 1, b: 1, a: 1}
    topRight: {r: 1, g: 1, b: 1, a: 1}
    bottomLeft: {r: 1, g: 1, b: 1, a: 1}
    bottomRight: {r: 1, g: 1, b: 1, a: 1}
  m_fontColorGradientPreset: {fileID: 0}
  m_spriteAsset: {fileID: 0}
  m_tintAllSprites: 0
  m_StyleSheet: {fileID: 0}
  m_TextStyleHashCode: -1183493901
  m_overrideHtmlColors: 0
  m_faceColor:
    serializedVersion: 2
    rgba: 4294967295
  m_fontSize: 14
  m_fontSizeBase: 14
  m_fontWeight: 400
  m_enableAutoSizing: 0
  m_fontSizeMin: 10
  m_fontSizeMax: 24
  m_fontStyle: 0
  m_HorizontalAlignment: 2
  m_VerticalAlignment: 512
  m_textAlignment: 65535
  m_characterSpacing: 0
  m_wordSpacing: 0
  m_lineSpacing: 0
  m_lineSpacingMax: 0
  m_paragraphSpacing: 0
  m_charWidthMaxAdj: 0
  m_TextWrappingMode: 0
  m_wordWrappingRatios: 0.4
  m_overflowMode: 0
  m_linkedTextComponent: {fileID: 0}
  parentLinkedComponent: {fileID: 0}
  m_enableKerning: 1
  m_ActiveFontFeatures: 6e72656b
  m_enableExtraPadding: 0
  checkPaddingRequired: 0
  m_isRichText: 1
  m_EmojiFallbackSupport: 1
  m_parseCtrlCharacters: 1
  m_isOrthographic: 1
  m_isCullingEnabled: 0
  m_horizontalMapping: 0
  m_verticalMapping: 0
  m_uvLineOffset: 0
  m_geometrySortingOrder: 0
  m_IsTextObjectScaleStatic: 0
  m_VertexBufferAutoSizeReduction: 0
  m_useMaxVisibleDescender: 1
  m_pageToDisplay: 1
  m_margin: {x: 0, y: 0, z: 0, w: 0}
  m_isUsingLegacyAnimationComponent: 0
  m_isVolumetricText: 0
  m_hasFontAssetChanged: 0
  m_baseMaterial: {fileID: 0}
  m_maskOffset: {x: 0, y: 0, z: 0, w: 0}
"""
    return [btn_icon, note_window, header_icon, note_title, note_subtitle, close_btn, div_top, suspect_lbl, div_bot, pool_lbl, bottom_hint]

def upgrade_scene(scene_name):
    scene_path = ROOT / f"Assets/Scenes/{scene_name}.unity"
    print(f"Upgrading {scene_name}...")
    
    backup_path = ROOT / f"Backups/{scene_name}.unity.pre_note_redesign"
    backup_path.parent.mkdir(exist_ok=True)
    shutil.copy2(scene_path, backup_path)
    
    content = scene_path.read_text(encoding="utf-8")
    
    # Notebook root: GO 1927024418, RectTransform 1927024419, Image 1927024420.
    old_rt_1927024419 = re.search(r'(--- !u!224 &1927024419\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_rt_1927024419 = """--- !u!224 &1927024419
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1927024418}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 7000000000000000511}
  m_Father: {fileID: 7000000000000000013}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}"""
    content = content.replace(old_rt_1927024419, new_rt_1927024419)
    
    # NotePanel Image (1927024420): dark atmospheric vignette dimmer
    old_img_1927024420 = re.search(r'(--- !u!114 &1927024420\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_img_1927024420 = """--- !u!114 &1927024420
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1927024418}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_Material: {fileID: 0}
  m_Color: {r: 0.04, g: 0.03, b: 0.03, a: 0.78}
  m_RaycastTarget: 1
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}
  m_Type: 1
  m_PreserveAspect: 0
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1"""
    content = content.replace(old_img_1927024420, new_img_1927024420)
    
    # Suspect row: GO 1583333836, RectTransform 1583333837, layout 1583333838.
    # RT 1583333837 father becomes NoteWindow RT 7000000000000000511
    old_rt_1583333837 = re.search(r'(--- !u!224 &1583333837\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_rt_1583333837 = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: 7000000000000000511}', old_rt_1583333837)
    new_rt_1583333837 = re.sub(r'm_AnchorMin: \{x: [^,]+, y: [^\}]+\}', 'm_AnchorMin: {x: 0.5, y: 0.5}', new_rt_1583333837)
    new_rt_1583333837 = re.sub(r'm_AnchorMax: \{x: [^,]+, y: [^\}]+\}', 'm_AnchorMax: {x: 0.5, y: 0.5}', new_rt_1583333837)
    new_rt_1583333837 = re.sub(r'm_AnchoredPosition: \{x: [^,]+, y: [^\}]+\}', 'm_AnchoredPosition: {x: 0, y: 135}', new_rt_1583333837)
    new_rt_1583333837 = re.sub(r'm_SizeDelta: \{x: [^,]+, y: [^\}]+\}', 'm_SizeDelta: {x: 1100, y: 120}', new_rt_1583333837)
    content = content.replace(old_rt_1583333837, new_rt_1583333837)
    
    # Panel HorizontalLayoutGroup (1583333838)
    old_hlg_1583333838 = re.search(r'(--- !u!114 &1583333838\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_hlg_1583333838 = re.sub(r'm_Spacing: \d+', 'm_Spacing: 36', old_hlg_1583333838)
    new_hlg_1583333838 = re.sub(r'm_ChildForceExpandWidth: \d', 'm_ChildForceExpandWidth: 0', new_hlg_1583333838)
    new_hlg_1583333838 = re.sub(r'm_ChildForceExpandHeight: \d', 'm_ChildForceExpandHeight: 0', new_hlg_1583333838)
    content = content.replace(old_hlg_1583333838, new_hlg_1583333838)
    
    # Panel Image (1583333839) -> Transparent background
    old_img_1583333839 = re.search(r'(--- !u!114 &1583333839\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_img_1583333839 = re.sub(r'm_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: [^\}]+\}', 'm_Color: {r: 0, g: 0, b: 0, a: 0}', old_img_1583333839)
    new_img_1583333839 = re.sub(r'm_RaycastTarget: \d', 'm_RaycastTarget: 0', new_img_1583333839)
    content = content.replace(old_img_1583333839, new_img_1583333839)
    
    # Token pool: GO 1537557984, RectTransform 1537557985, grid 1537557986.
    old_rt_1537557985 = re.search(r'(--- !u!224 &1537557985\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_rt_1537557985 = re.sub(r'm_Father: \{fileID: \d+\}', 'm_Father: {fileID: 7000000000000000511}', old_rt_1537557985)
    new_rt_1537557985 = re.sub(r'm_AnchorMin: \{x: [^,]+, y: [^\}]+\}', 'm_AnchorMin: {x: 0.5, y: 0.5}', new_rt_1537557985)
    new_rt_1537557985 = re.sub(r'm_AnchorMax: \{x: [^,]+, y: [^\}]+\}', 'm_AnchorMax: {x: 0.5, y: 0.5}', new_rt_1537557985)
    new_rt_1537557985 = re.sub(r'm_AnchoredPosition: \{x: [^,]+, y: [^\}]+\}', 'm_AnchoredPosition: {x: 0, y: -135}', new_rt_1537557985)
    new_rt_1537557985 = re.sub(r'm_SizeDelta: \{x: [^,]+, y: [^\}]+\}', 'm_SizeDelta: {x: 1100, y: 240}', new_rt_1537557985)
    content = content.replace(old_rt_1537557985, new_rt_1537557985)
    
    # PoolArea GridLayoutGroup (1537557986): 8 cols x 2 rows
    old_glg_1537557986 = re.search(r'(--- !u!114 &1537557986\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_glg_1537557986 = re.sub(r'm_CellSize: \{x: [^,]+, y: [^\}]+\}', 'm_CellSize: {x: 96, y: 96}', old_glg_1537557986)
    new_glg_1537557986 = re.sub(r'm_Spacing: \{x: [^,]+, y: [^\}]+\}', 'm_Spacing: {x: 32, y: 24}', new_glg_1537557986)
    new_glg_1537557986 = re.sub(r'm_Constraint: \d', 'm_Constraint: 1', new_glg_1537557986)
    new_glg_1537557986 = re.sub(r'm_ConstraintCount: \d+', 'm_ConstraintCount: 8', new_glg_1537557986)
    content = content.replace(old_glg_1537557986, new_glg_1537557986)
    
    # PoolArea Image (1537557987) -> Transparent
    old_img_1537557987 = re.search(r'(--- !u!114 &1537557987\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_img_1537557987 = re.sub(r'm_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: [^\}]+\}', 'm_Color: {r: 0, g: 0, b: 0, a: 0}', old_img_1537557987)
    new_img_1537557987 = re.sub(r'm_RaycastTarget: \d', 'm_RaycastTarget: 0', new_img_1537557987)
    content = content.replace(old_img_1537557987, new_img_1537557987)
    
    # Apply the slot artwork and dimensions to suspect seats and the token pool.
    # Each slot uses a 96 × 96 RectTransform and note-slot.png.
    for parent_rt, slot_count in [(1583333837, 6), (1537557985, 16)]:
        rt_body = re.search(rf'--- !u!224 &{parent_rt}\n(.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
        slot_rt_ids = re.findall(r'- \{fileID: (\d+)\}', rt_body.split('m_Father:')[0])
        print(f"  Found {len(slot_rt_ids)} slots for parent {parent_rt}")
        for s_rt in slot_rt_ids:
            old_srt = re.search(rf'(--- !u!224 &{s_rt}\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
            new_srt = re.sub(r'm_SizeDelta: \{x: [^,]+, y: [^\}]+\}', 'm_SizeDelta: {x: 96, y: 96}', old_srt)
            content = content.replace(old_srt, new_srt)
            
            s_go_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', old_srt).group(1)
            s_go_body = re.search(rf'--- !u!1 &{s_go_id}\n(.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
            comps = re.findall(r'- component: \{fileID: (\d+)\}', s_go_body)
            for c in comps:
                c_body = re.search(rf'--- !u!(\d+) &{c}\n(.*?)(?=\n--- !u!)', content, re.DOTALL)
                if c_body and c_body.group(1) == '114' and f'guid: {IMAGE_GUID},' in c_body.group(2):
                    old_s_img = c_body.group(0)
                    new_s_img = re.sub(r'm_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: [^\}]+\}', 'm_Color: {r: 1, g: 1, b: 1, a: 1}', old_s_img)
                    new_s_img = re.sub(r'm_Sprite: \{fileID: [^,]+, guid: [^,]+, type: \d+\}|m_Sprite: \{fileID: 0\}', f'm_Sprite: {{fileID: 21300000, guid: {IMG_SLOT_GUID}, type: 3}}', new_s_img)
                    new_s_img = re.sub(r'm_Type: \d', 'm_Type: 0', new_s_img)
                    new_s_img = re.sub(r'm_RaycastTarget: \d', 'm_RaycastTarget: 1', new_s_img)
                    content = content.replace(old_s_img, new_s_img)

    # Apply the token style to all sixteen numbered blocks and their labels.
    pool_rt_body = re.search(r'--- !u!224 &1537557985\n(.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    slot_rt_ids = re.findall(r'- \{fileID: (\d+)\}', pool_rt_body.split('m_Father:')[0])
    for s_rt in slot_rt_ids:
        s_rt_body = re.search(rf'--- !u!224 &{s_rt}\n(.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
        block_rt_ids = re.findall(r'- \{fileID: (\d+)\}', s_rt_body.split('m_Father:')[0])
        for b_rt in block_rt_ids:
            old_brt = re.search(rf'(--- !u!224 &{b_rt}\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
            new_brt = re.sub(r'm_SizeDelta: \{x: [^,]+, y: [^\}]+\}', 'm_SizeDelta: {x: 86, y: 86}', old_brt)
            content = content.replace(old_brt, new_brt)
            
            b_go_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', old_brt).group(1)
            b_go_body = re.search(rf'--- !u!1 &{b_go_id}\n(.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
            comps = re.findall(r'- component: \{fileID: (\d+)\}', b_go_body)
            for c in comps:
                c_body = re.search(rf'--- !u!(\d+) &{c}\n(.*?)(?=\n--- !u!)', content, re.DOTALL)
                if c_body and c_body.group(1) == '114' and f'guid: {IMAGE_GUID},' in c_body.group(2):
                    old_b_img = c_body.group(0)
                    new_b_img = re.sub(r'm_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: [^\}]+\}', 'm_Color: {r: 1, g: 1, b: 1, a: 1}', old_b_img)
                    new_b_img = re.sub(r'm_Sprite: \{fileID: [^,]+, guid: [^,]+, type: \d+\}|m_Sprite: \{fileID: 0\}', f'm_Sprite: {{fileID: 21300000, guid: {IMG_TOKEN_GUID}, type: 3}}', new_b_img)
                    new_b_img = re.sub(r'm_Type: \d', 'm_Type: 0', new_b_img)
                    content = content.replace(old_b_img, new_b_img)
            
            txt_rt_ids = re.findall(r'- \{fileID: (\d+)\}', old_brt.split('m_Father:')[0])
            for t_rt in txt_rt_ids:
                old_trt = re.search(rf'(--- !u!224 &{t_rt}\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
                new_trt = re.sub(r'm_SizeDelta: \{x: [^,]+, y: [^\}]+\}', 'm_SizeDelta: {x: 86, y: 86}', old_trt)
                content = content.replace(old_trt, new_trt)
                
                t_go_id = re.search(r'm_GameObject: \{fileID: (\d+)\}', old_trt).group(1)
                t_go_body = re.search(rf'--- !u!1 &{t_go_id}\n(.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
                t_comps = re.findall(r'- component: \{fileID: (\d+)\}', t_go_body)
                for tc in t_comps:
                    tc_body = re.search(rf'--- !u!(\d+) &{tc}\n(.*?)(?=\n--- !u!)', content, re.DOTALL)
                    if tc_body and tc_body.group(1) == '114' and f'guid: {TMP_GUID},' in tc_body.group(2):
                        old_tmp = tc_body.group(0)
                        new_tmp = re.sub(r'm_fontSize: \d+', 'm_fontSize: 40', old_tmp)
                        new_tmp = re.sub(r'm_fontSizeBase: \d+', 'm_fontSizeBase: 40', new_tmp)
                        new_tmp = re.sub(r'm_fontColor: \{r: [^,]+, g: [^,]+, b: [^,]+, a: [^\}]+\}', 'm_fontColor: {r: 0.972549, g: 0.92549, b: 0.835294, a: 1}', new_tmp)
                        new_tmp = re.sub(r'm_fontColor32:\s*serializedVersion: 2\s*rgba: \d+', 'm_fontColor32:\n    serializedVersion: 2\n    rgba: 4291880439', new_tmp)
                        new_tmp = re.sub(r'm_VerticalAlignment: \d+', 'm_VerticalAlignment: 512', new_tmp)
                        content = content.replace(old_tmp, new_tmp)

    # Notes button: GO 7000000000000000050, RectTransform 7000000000000000051.
    old_btn_rt = re.search(r'(--- !u!224 &7000000000000000051\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    if '7000000000000000502' not in old_btn_rt:
        new_btn_rt = re.sub(r'(m_Children:\n)', r'\1  - {fileID: 7000000000000000502}\n', old_btn_rt)
        content = content.replace(old_btn_rt, new_btn_rt)
    
    old_lbl_rt = re.search(r'(--- !u!224 &7000000000000000056\n.*?)(?=\n--- !u!)', content, re.DOTALL).group(1)
    new_lbl_rt = re.sub(r'm_AnchoredPosition: \{x: [^,]+, y: [^\}]+\}', 'm_AnchoredPosition: {x: 52, y: 0}', old_lbl_rt)
    new_lbl_rt = re.sub(r'm_SizeDelta: \{x: [^,]+, y: [^\}]+\}', 'm_SizeDelta: {x: 176, y: 46}', new_lbl_rt)
    content = content.replace(old_lbl_rt, new_lbl_rt)
    
    new_blocks = build_yaml_blocks()
    for block in new_blocks:
        b_id = re.search(r'&(\d+)', block).group(1)
        if f'&{b_id}' not in content:
            content += "\n" + block.strip() + "\n"
    
    scene_path.write_text(content, encoding="utf-8")
    print(f"Successfully upgraded {scene_name}!")

if __name__ == "__main__":
    upgrade_scene("MainBoard")
    upgrade_scene("TutorialBoard")

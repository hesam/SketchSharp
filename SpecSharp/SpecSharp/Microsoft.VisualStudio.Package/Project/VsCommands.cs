//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
using System;

namespace Microsoft.VisualStudio.Package{
  // VsConstants.guidStandardCommandSet97 
  public enum VsCommands {
    AlignBottom = 1,                                                                    
    AlignHorizontalCenters = 2,                                                         
    AlignLeft = 3,                                                                      
    AlignRight = 4,                                                                     
    AlignToGrid = 5,                                                                    
    AlignTop = 6,                                                                       
    AlignVerticalCenters = 7,                                                           
    ArrangeBottom = 8,                                                                  
    ArrangeRight = 9,                                                                   
    BringForward = 10,                                                                  
    BringToFront = 11,                                                                  
    CenterHorizontally = 12,                                                            
    CenterVertically = 13,                                                              
    Code = 14,                                                                          
    Copy = 15,                                                                          
    Cut = 16,                                                                           
    Delete = 17,                                                                        
    FontName = 18,                                                                      
    FontNameGetList = 500,                                                                      
    FontSize = 19,                                                                      
    FontSizeGetList = 501,                                                                      
    Group = 20,                                                                         
    HorizSpaceConcatenate = 21,                                                         
    HorizSpaceDecrease = 22,                                                            
    HorizSpaceIncrease = 23,                                                            
    HorizSpaceMakeEqual = 24,                                                           
    LockControls = 369, 
    InsertObject = 25,                                                                  
    Paste = 26,                                                                         
    Print = 27,                                                                         
    Properties = 28,                                                                    
    Redo = 29,                                                                          
    MultiLevelRedo = 30,                                                                
    SelectAll = 31,                                                                     
    SendBackward = 32,                                                                  
    SendToBack = 33,                                                                    
    ShowTable = 34,                                                                     
    SizeToControl = 35,                                                                 
    SizeToControlHeight = 36,                                                           
    SizeToControlWidth = 37,                                                            
    SizeToFit = 38,                                                                     
    SizeToGrid = 39,                                                                    
    SnapToGrid = 40,                                                                    
    TabOrder = 41,                                                                      
    Toolbox = 42,                                                                       
    Undo = 43,                                                                          
    MultiLevelUndo = 44,                                                                
    Ungroup = 45,                                                                       
    VertSpaceConcatenate = 46,                                                          
    VertSpaceDecrease = 47,                                                             
    VertSpaceIncrease = 48,                                                             
    VertSpaceMakeEqual = 49,                                                            
    ZoomPercent = 50,                                                                   
    BackColor = 51,                                                                     
    Bold = 52,                                                                          
    BorderColor = 53,                                                                   
    BorderDashDot = 54,                                                                 
    BorderDashDotDot = 55,                                                              
    BorderDashes = 56,                                                                  
    BorderDots = 57,                                                                    
    BorderShortDashes = 58,                                                             
    BorderSolid = 59,                                                                   
    BorderSparseDots = 60,                                                              
    BorderWidth1 = 61,                                                                  
    BorderWidth2 = 62,                                                                  
    BorderWidth3 = 63,                                                                  
    BorderWidth4 = 64,                                                                  
    BorderWidth5 = 65,                                                                  
    BorderWidth6 = 66,                                                                  
    BorderWidthHairline = 67,                                                           
    Flat = 68,                                                                          
    ForeColor = 69,                                                                     
    Italic = 70,                                                                        
    JustifyCenter = 71,                                                                 
    JustifyGeneral = 72,                                                                
    JustifyLeft = 73,                                                                   
    JustifyRight = 74,                                                                  
    Raised = 75,                                                                        
    Sunken = 76,                                                                        
    Underline = 77,                                                                     
    Chiseled = 78,                                                                      
    Etched = 79,                                                                        
    Shadowed = 80,                                                                      
    CompDebug1 = 81,                                                                    
    CompDebug2 = 82,                                                                    
    CompDebug3 = 83,                                                                    
    CompDebug4 = 84,                                                                    
    CompDebug5 = 85,                                                                    
    CompDebug6 = 86,                                                                    
    CompDebug7 = 87,                                                                    
    CompDebug8 = 88,                                                                    
    CompDebug9 = 89,                                                                    
    CompDebug10 = 90,                                                                   
    CompDebug11 = 91,                                                                   
    CompDebug12 = 92,                                                                   
    CompDebug13 = 93,                                                                   
    CompDebug14 = 94,                                                                   
    CompDebug15 = 95,                                                                   
    ExistingSchemaEdit = 96,                                                            
    Find = 97,                                                                          
    GetZoom = 98,                                                                       
    QueryOpenDesign = 99,                                                               
    QueryOpenNew = 100,                                                                 
    SingleTableDesign = 101,                                                            
    SingleTableNew = 102,                                                               
    ShowGrid = 103, 
    NewTable = 104, 
    CollapsedView = 105, 
    FieldView = 106, 
    VerifySQL = 107, 
    HideTable = 108, 

    PrimaryKey = 109, 
    Save = 110, 
    SaveAs = 111, 
    SortAscending = 112, 

    SortDescending = 113, 
    AppendQuery = 114, 
    CrosstabQuery = 115, 
    DeleteQuery = 116, 
    MakeTableQuery = 117, 

    SelectQuery = 118, 
    UpdateQuery = 119, 
    Parameters = 120, 
    Totals = 121, 
    ViewCollapsed = 122, 

    ViewFieldList = 123, 


    ViewKeys = 124, 
    ViewGrid = 125, 
    InnerJoin = 126, 

    RightOuterJoin = 127, 
    LeftOuterJoin = 128, 
    FullOuterJoin = 129, 
    UnionJoin = 130, 
    ShowSQLPane = 131, 

    ShowGraphicalPane = 132, 
    ShowDataPane = 133, 
    ShowQBEPane = 134, 
    SelectAllFields = 135, 

    OLEObjectMenuButton = 136, 

    // ids on the ole verbs menu - these must be sequential ie verblist0-verblist9
    ObjectVerbList0 = 137, 
    ObjectVerbList1 = 138, 
    ObjectVerbList2 = 139, 
    ObjectVerbList3 = 140, 
    ObjectVerbList4 = 141, 
    ObjectVerbList5 = 142, 
    ObjectVerbList6 = 143, 
    ObjectVerbList7 = 144, 
    ObjectVerbList8 = 145, 
    ObjectVerbList9 = 146,  // Unused on purpose!

    ConvertObject = 147, 
    CustomControl = 148, 
    CustomizeItem = 149, 
    Rename = 150, 

    Import = 151, 
    NewPage = 152, 
    Move = 153, 
    Cancel = 154, 

    Font = 155, 

    ExpandLinks = 156, 
    ExpandImages = 157, 
    ExpandPages = 158, 
    RefocusDiagram = 159, 
    TransitiveClosure = 160, 
    CenterDiagram = 161, 
    ZoomIn = 162, 
    ZoomOut = 163, 
    RemoveFilter = 164, 
    HidePane = 165, 
    DeleteTable = 166, 
    DeleteRelationship = 167, 
    Remove = 168, 
    JoinLeftAll = 169, 
    JoinRightAll = 170, 
    AddToOutput = 171,  	// Add selected fields to query output
    OtherQuery = 172,  	// change query type to 'other'
    GenerateChangeScript = 173, 
    SaveSelection = 174, 	// Save current selection
    AutojoinCurrent = 175, 	// Autojoin current tables
    AutojoinAlways = 176, 	// Toggle Autojoin state
    EditPage = 177, 	// Launch editor for url
    ViewLinks = 178, 	// Launch new webscope for url
    Stop = 179, 	// Stope webscope rendering
    Pause = 180, 	// Pause webscope rendering
    Resume = 181, 	// Resume webscope rendering
    FilterDiagram = 182, 	// Filter webscope diagram
    ShowAllObjects = 183, 	// Show All objects in webscope diagram
    ShowApplications = 184, 	// Show Application objects in webscope diagram
    ShowOtherObjects = 185, 	// Show other objects in webscope diagram
    ShowPrimRelationships = 186, 	// Show primary relationships
    Expand = 187, 	// Expand links
    Collapse = 188, 	// Collapse links
    Refresh = 189, 	// Refresh Webscope diagram
    Layout = 190, 	// Layout websope diagram
    ShowResources = 191, 	// Show resouce objects in webscope diagram
    InsertHTMLWizard = 192, 	// Insert HTML using a Wizard
    ShowDownloads = 193, 	// Show download objects in webscope diagram
    ShowExternals = 194, 	// Show external objects in webscope diagram
    ShowInBoundLinks = 195, 	// Show inbound links in webscope diagram
    ShowOutBoundLinks = 196, 	// Show out bound links in webscope diagram
    ShowInAndOutBoundLinks = 197, 	// Show in and out bound links in webscope diagram
    Preview = 198, 	// Preview page
    Open = 261, 	// Open
    OpenWith = 199, 	// Open with
    ShowPages = 200, 	// Show HTML pages
    RunQuery = 201,  	// Runs a query
    ClearQuery = 202,  	// Clears the query's associated cursor
    RecordFirst = 203,  	// Go to first record in set
    RecordLast = 204,  	// Go to last record in set
    RecordNext = 205,  	// Go to next record in set
    RecordPrevious = 206,  	// Go to previous record in set
    RecordGoto = 207,  	// Go to record via dialog
    RecordNew = 208,  	// Add a record to set

    InsertNewMenu = 209, 	// menu designer
    InsertSeparator = 210, 	// menu designer
    EditMenuNames = 211, 	// menu designer

    DebugExplorer = 212,  
    DebugProcesses = 213, 
    ViewThreadsWindow = 214, 
    WindowUIList = 215, 

    // ids on the file menu
    NewProject = 216, 
    OpenProject = 217, 
    OpenProjectFromWeb = 450, 
    OpenSolution = 218, 
    CloseSolution = 219, 
    FileNew = 221, 
    FileOpen = 222, 
    FileOpenFromWeb = 451, 
    FileClose = 223, 
    SaveSolution = 224, 
    SaveSolutionAs = 225, 
    SaveProjectItemAs = 226, 
    PageSetup = 227, 
    PrintPreview = 228, 
    Exit = 229, 

    // ids on the edit menu
    Replace = 230, 
    Goto = 231, 

    // ids on the view menu
    PropertyPages = 232, 
    FullScreen = 233, 
    ProjectExplorer = 234, 
    PropertiesWindow = 235, 
    TaskListWindow = 236, 
    OutputWindow = 237, 
    ObjectBrowser = 238, 
    DocOutlineWindow = 239, 
    ImmediateWindow = 240, 
    WatchWindow = 241, 
    LocalsWindow = 242, 
    CallStack = 243, 
    AutosWindow = DebugReserved1, 
    ThisWindow = DebugReserved2, 

    // ids on project menu
    AddNewItem = 220, 
    AddExistingItem = 244, 
    NewFolder = 245, 
    SetStartupProject = 246, 
    ProjectSettings = 247, 
    ProjectReferences = 367, 

    // ids on the debug menu
    StepInto = 248, 
    StepOver = 249, 
    StepOut = 250, 
    RunToCursor = 251, 
    AddWatch = 252, 
    EditWatch = 253, 
    QuickWatch = 254, 

    ToggleBreakpoint = 255, 
    ClearBreakpoints = 256, 
    ShowBreakpoints = 257, 
    SetNextStatement = 258, 
    ShowNextStatement = 259, 
    EditBreakpoint = 260, 
    DetachDebugger = 262, 

    // ids on the tools menu
    CustomizeKeyboard = 263, 
    ToolsOptions = 264, 

    // ids on the windows menu
    NewWindow = 265, 
    Split = 266, 
    Cascade = 267, 
    TileHorz = 268, 
    TileVert = 269, 

    // ids on the help menu
    TechSupport = 270, 

    // NOTE cmdidAbout and cmdidDebugOptions must be consecutive
    //      cmd after cmdidDebugOptions (ie 273) must not be used
    About = 271, 
    DebugOptions = 272, 

    // ids on the watch context menu
    // CollapseWatch appears as 'Collapse Parent', on any
    // non-top-level item
    DeleteWatch = 274, 
    CollapseWatch = 275, 
    // ids 276, 277, 278, 279, 280 are in use
    // below 
    // ids on the property browser context menu
    PbrsToggleStatus = 282, 
    PropbrsHide = 283, 

    // ids on the docking context menu
    DockingView = 284, 
    HideActivePane = 285, 
    // ids for window selection via keyboard
    PaneNextPane = 316,  //(listed below in order)
    PanePrevPane = 317,  //(listed below in order)
    PaneNextTab = 286, 
    PanePrevTab = 287, 
    PaneCloseToolWindow = 288, 
    PaneActivateDocWindow = 289, 
    DockingViewMDI = 290, 
    DockingViewFloater = 291, 
    AutoHideWindow = 292, 
    MoveToDropdownBar = 293, 
    FindCmd = 294,  // internal Find commands
    Start = 295, 
    Restart = 296, 

    AddinManager = 297, 

    MultiLevelUndoList = 298, 
    MultiLevelRedoList = 299, 

    ToolboxAddTab = 300, 
    ToolboxDeleteTab = 301, 
    ToolboxRenameTab = 302,   
    ToolboxTabMoveUp = 303, 
    ToolboxTabMoveDown = 304, 
    ToolboxRenameItem = 305, 
    ToolboxListView = 306, 
    //(below) cmdidSearchSetCombo		307

    WindowUIGetList = 308, 
    InsertValuesQuery = 309, 

    ShowProperties = 310, 

    ThreadSuspend = 311, 
    ThreadResume = 312, 
    ThreadSetFocus = 313, 
    DisplayRadix = 314, 

    OpenProjectItem = 315, 

    ClearPane = 318, 
    GotoErrorTag = 319, 

    TaskListSortByCategory = 320, 
    TaskListSortByFileLine = 321, 
    TaskListSortByPriority = 322, 
    TaskListSortByDefaultSort = 323, 
    TaskListShowTooltip = 324, 
    TaskListFilterByNothing = 325, 
    CancelEZDrag = 326, 
    TaskListFilterByCategoryCompiler = 327, 
    TaskListFilterByCategoryComment = 328, 

    ToolboxAddItem = 329, 
    ToolboxReset = 330, 

    SaveProjectItem = 331, 
    SaveOptions = 959, 
    ViewForm = 332, 
    ViewCode = 333, 
    PreviewInBrowser = 334, 
    BrowseWith = 336, 
    SearchSetCombo = 307, 
    SearchCombo = 337, 
    EditLabel = 338, 
    Exceptions = 339, 
    DefineViews = 340, 

    ToggleSelMode = 341, 
    ToggleInsMode = 342, 

    LoadUnloadedProject = 343, 
    UnloadLoadedProject = 344, 

    // ids on the treegrids (watch/local/threads/stack)
    ElasticColumn = 345, 
    HideColumn = 346, 

    TaskListPreviousView = 347, 
    ZoomDialog = 348, 

    // find/replace options
    FindHiddenText = 349, 
    FindMatchCase = 350, 
    FindWholeWord = 351, 
    FindSimplePattern = 276, 
    FindRegularExpression = 352, 
    FindBackwards = 353, 
    FindInSelection = 354, 
    FindStop = 355, 
    // UNUSED                               356
    FindInFiles = 277, 
    ReplaceInFiles = 278, 
    NextLocation = 279,  // next item in task list, find in files results, etc.
    PreviousLocation = 280,  // prev item "

    TaskListNextError = 357, 
    TaskListPrevError = 358, 
    TaskListFilterByCategoryUser = 359, 
    TaskListFilterByCategoryShortcut = 360, 
    TaskListFilterByCategoryHTML = 361, 
    TaskListFilterByCurrentFile = 362, 
    TaskListFilterByChecked = 363, 
    TaskListFilterByUnchecked = 364, 
    TaskListSortByDescription = 365, 
    TaskListSortByChecked = 366, 

    // 367 is used above in cmdidProjectReferences
    StartNoDebug = 368, 
    // 369 is used above in cmdidLockControls

    FindNext = 370, 
    FindPrev = 371, 
    FindSelectedNext = 372, 
    FindSelectedPrev = 373, 
    SearchGetList = 374, 
    InsertBreakpoint = 375, 
    EnableBreakpoint = 376, 
    F1Help = 377, 

    //UNUSED 378-396

    MoveToNextEZCntr = 384, 
    MoveToPreviousEZCntr = 393, 

    ProjectProperties = 396, 
    PropSheetOrProperties = 397, 

    // NOTE - the next items are debug only !!
    TshellStep = 398, 
    TshellRun = 399, 

    // marker commands on the codewin menu
    MarkerCmd0 = 400, 
    MarkerCmd1 = 401, 
    MarkerCmd2 = 402, 
    MarkerCmd3 = 403, 
    MarkerCmd4 = 404, 
    MarkerCmd5 = 405, 
    MarkerCmd6 = 406, 
    MarkerCmd7 = 407, 
    MarkerCmd8 = 408, 
    MarkerCmd9 = 409, 
    MarkerLast = 409, 
    MarkerEnd = 410,  // list terminator reserved

    // user-invoked project reload and unload
    ReloadProject = 412, 
    UnloadProject = 413, 

    NewBlankSolution = 414, 
    SelectProjectTemplate = 415, 

    // document outline commands
    DetachAttachOutline = 420, 
    ShowHideOutline = 421, 
    SyncOutline = 422, 

    RunToCallstCursor = 423, 
    NoCmdsAvailable = 424, 

    ContextWindow = 427, 
    Alias = 428, 
    GotoCommandLine = 429, 
    EvaluateExpression = 430, 
    ImmediateMode = 431, 
    EvaluateStatement = 432, 

    FindResultWindow1 = 433, 
    FindResultWindow2 = 434, 

    // 500 is used above in cmdidFontNameGetList
    // 501 is used above in cmdidFontSizeGetList

    // ids on the window menu - these must be sequential ie window1-morewind
    Window1 = 570, 
    Window2 = 571, 
    Window3 = 572, 
    Window4 = 573, 
    Window5 = 574, 
    Window6 = 575, 
    Window7 = 576, 
    Window8 = 577, 
    Window9 = 578, 
    Window10 = 579, 
    Window11 = 580, 
    Window12 = 581, 
    Window13 = 582, 
    Window14 = 583, 
    Window15 = 584, 
    Window16 = 585, 
    Window17 = 586, 
    Window18 = 587, 
    Window19 = 588, 
    Window20 = 589, 
    Window21 = 590, 
    Window22 = 591, 
    Window23 = 592, 
    Window24 = 593, 
    Window25 = 594,    // note cmdidWindow25 is unused on purpose!
    MoreWindows = 595, 

    AutoHideAllWindows = 597,   
    TaskListTaskHelp = 598, 

    ClassView = 599, 

    MRUProj1 = 600, 
    MRUProj2 = 601, 
    MRUProj3 = 602, 
    MRUProj4 = 603, 
    MRUProj5 = 604, 
    MRUProj6 = 605, 
    MRUProj7 = 606, 
    MRUProj8 = 607, 
    MRUProj9 = 608, 
    MRUProj10 = 609, 
    MRUProj11 = 610, 
    MRUProj12 = 611, 
    MRUProj13 = 612, 
    MRUProj14 = 613, 
    MRUProj15 = 614, 
    MRUProj16 = 615, 
    MRUProj17 = 616, 
    MRUProj18 = 617, 
    MRUProj19 = 618, 
    MRUProj20 = 619, 
    MRUProj21 = 620, 
    MRUProj22 = 621, 
    MRUProj23 = 622, 
    MRUProj24 = 623, 
    MRUProj25 = 624,   // note cmdidMRUProj25 is unused on purpose!

    SplitNext = 625, 
    SplitPrev = 626, 

    CloseAllDocuments = 627, 
    NextDocument = 628, 
    PrevDocument = 629, 

    Tool1 = 630,   // note cmdidTool1 - cmdidTool24 must be
    Tool2 = 631,   // consecutive
    Tool3 = 632, 
    Tool4 = 633, 
    Tool5 = 634, 
    Tool6 = 635, 
    Tool7 = 636, 
    Tool8 = 637, 
    Tool9 = 638, 
    Tool10 = 639, 
    Tool11 = 640, 
    Tool12 = 641, 
    Tool13 = 642, 
    Tool14 = 643, 
    Tool15 = 644, 
    Tool16 = 645, 
    Tool17 = 646, 
    Tool18 = 647, 
    Tool19 = 648, 
    Tool20 = 649, 
    Tool21 = 650, 
    Tool22 = 651, 
    Tool23 = 652, 
    Tool24 = 653, 
    ExternalCommands = 654, 

    PasteNextTBXCBItem = 655, 
    ToolboxShowAllTabs = 656, 
    ProjectDependencies = 657, 
    CloseDocument = 658, 
    ToolboxSortItems = 659, 

    ViewBarView1 = 660,    //UNUSED
    ViewBarView2 = 661,    //UNUSED
    ViewBarView3 = 662,    //UNUSED
    ViewBarView4 = 663,    //UNUSED
    ViewBarView5 = 664,    //UNUSED
    ViewBarView6 = 665,    //UNUSED
    ViewBarView7 = 666,    //UNUSED
    ViewBarView8 = 667,    //UNUSED
    ViewBarView9 = 668,    //UNUSED
    ViewBarView10 = 669,    //UNUSED
    ViewBarView11 = 670,    //UNUSED
    ViewBarView12 = 671,    //UNUSED
    ViewBarView13 = 672,    //UNUSED
    ViewBarView14 = 673,    //UNUSED
    ViewBarView15 = 674,    //UNUSED
    ViewBarView16 = 675,    //UNUSED
    ViewBarView17 = 676,    //UNUSED
    ViewBarView18 = 677,    //UNUSED
    ViewBarView19 = 678,    //UNUSED
    ViewBarView20 = 679,    //UNUSED
    ViewBarView21 = 680,    //UNUSED
    ViewBarView22 = 681,    //UNUSED
    ViewBarView23 = 682,    //UNUSED
    ViewBarView24 = 683,    //UNUSED

    SolutionCfg = 684, 
    SolutionCfgGetList = 685, 

    //
    // Schema table commands:
    // All invoke table property dialog and select appropriate page.
    //
    ManageIndexes = 675, 
    ManageRelationships = 676, 
    ManageConstraints = 677, 

    TaskListCustomView1 = 678, 
    TaskListCustomView2 = 679, 
    TaskListCustomView3 = 680, 
    TaskListCustomView4 = 681, 
    TaskListCustomView5 = 682, 
    TaskListCustomView6 = 683, 
    TaskListCustomView7 = 684, 
    TaskListCustomView8 = 685, 
    TaskListCustomView9 = 686, 
    TaskListCustomView10 = 687, 
    TaskListCustomView11 = 688, 
    TaskListCustomView12 = 689, 
    TaskListCustomView13 = 690, 
    TaskListCustomView14 = 691, 
    TaskListCustomView15 = 692, 
    TaskListCustomView16 = 693,   
    TaskListCustomView17 = 694, 
    TaskListCustomView18 = 695, 
    TaskListCustomView19 = 696, 
    TaskListCustomView20 = 697, 
    TaskListCustomView21 = 698, 
    TaskListCustomView22 = 699, 
    TaskListCustomView23 = 700, 
    TaskListCustomView24 = 701, 
    TaskListCustomView25 = 702, 
    TaskListCustomView26 = 703,  
    TaskListCustomView27 = 704, 
    TaskListCustomView28 = 705, 
    TaskListCustomView29 = 706, 
    TaskListCustomView30 = 707, 
    TaskListCustomView31 = 708, 
    TaskListCustomView32 = 709, 
    TaskListCustomView33 = 710, 
    TaskListCustomView34 = 711, 
    TaskListCustomView35 = 712, 
    TaskListCustomView36 = 713, 
    TaskListCustomView37 = 714, 
    TaskListCustomView38 = 715, 
    TaskListCustomView39 = 716, 
    TaskListCustomView40 = 717, 
    TaskListCustomView41 = 718, 
    TaskListCustomView42 = 719, 
    TaskListCustomView43 = 720, 
    TaskListCustomView44 = 721, 
    TaskListCustomView45 = 722, 
    TaskListCustomView46 = 723, 
    TaskListCustomView47 = 724, 
    TaskListCustomView48 = 725, 
    TaskListCustomView49 = 726, 
    TaskListCustomView50 = 727,  //not used on purpose, ends the list

    WhiteSpace = 728, 

    CommandWindow = 729, 
    CommandWindowMarkMode = 730, 
    LogCommandWindow = 731, 

    Shell = 732, 

    SingleChar = 733, 
    ZeroOrMore = 734, 
    OneOrMore = 735, 
    BeginLine = 736, 
    EndLine = 737, 
    BeginWord = 738, 
    EndWord = 739, 
    CharInSet = 740, 
    CharNotInSet = 741, 
    Or = 742, 
    Escape = 743, 
    TagExp = 744, 

    // Regex builder context help menu commands
    PatternMatchHelp = 745, 
    RegExList = 746, 

    DebugReserved1 = 747, 
    DebugReserved2 = 748, 
    DebugReserved3 = 749, 
    //USED ABOVE                        750
    //USED ABOVE                        751
    //USED ABOVE                        752
    //USED ABOVE                        753

    //Regex builder wildcard menu commands
    WildZeroOrMore = 754, 
    WildSingleChar = 755, 
    WildSingleDigit = 756, 
    WildCharInSet = 757, 
    WildCharNotInSet = 758, 

    FindWhatText = 759, 
    TaggedExp1 = 760, 
    TaggedExp2 = 761, 
    TaggedExp3 = 762, 
    TaggedExp4 = 763, 
    TaggedExp5 = 764, 
    TaggedExp6 = 765, 
    TaggedExp7 = 766, 
    TaggedExp8 = 767, 
    TaggedExp9 = 768, 		                    

    EditorWidgetClick = 769,  // param 0 is the moniker as VT_BSTR, param 1 is the buffer line as VT_I4, and param 2 is the buffer index as VT_I4
    CmdWinUpdateAC = 770, 

    SlnCfgMgr = 771, 

    AddNewProject = 772, 
    AddExistingProject = 773, 
    AddExistingProjFromWeb = 774, 

    AutoHideContext1 = 776, 
    AutoHideContext2 = 777, 
    AutoHideContext3 = 778, 
    AutoHideContext4 = 779, 
    AutoHideContext5 = 780, 
    AutoHideContext6 = 781, 
    AutoHideContext7 = 782, 
    AutoHideContext8 = 783, 
    AutoHideContext9 = 784, 
    AutoHideContext10 = 785, 
    AutoHideContext11 = 786, 
    AutoHideContext12 = 787, 
    AutoHideContext13 = 788, 
    AutoHideContext14 = 789, 
    AutoHideContext15 = 790, 
    AutoHideContext16 = 791, 
    AutoHideContext17 = 792, 
    AutoHideContext18 = 793, 
    AutoHideContext19 = 794, 
    AutoHideContext20 = 795, 
    AutoHideContext21 = 796, 
    AutoHideContext22 = 797, 
    AutoHideContext23 = 798, 
    AutoHideContext24 = 799, 
    AutoHideContext25 = 800, 
    AutoHideContext26 = 801, 
    AutoHideContext27 = 802, 
    AutoHideContext28 = 803, 
    AutoHideContext29 = 804, 
    AutoHideContext30 = 805, 
    AutoHideContext31 = 806, 
    AutoHideContext32 = 807, 
    AutoHideContext33 = 808,   // must remain unused

    ShellNavBackward = 809, 
    ShellNavForward = 810, 

    ShellNavigate1 = 811, 
    ShellNavigate2 = 812, 
    ShellNavigate3 = 813, 
    ShellNavigate4 = 814, 
    ShellNavigate5 = 815, 
    ShellNavigate6 = 816, 
    ShellNavigate7 = 817, 
    ShellNavigate8 = 818, 
    ShellNavigate9 = 819, 
    ShellNavigate10 = 820, 
    ShellNavigate11 = 821, 
    ShellNavigate12 = 822, 
    ShellNavigate13 = 823, 
    ShellNavigate14 = 824, 
    ShellNavigate15 = 825, 
    ShellNavigate16 = 826, 
    ShellNavigate17 = 827, 
    ShellNavigate18 = 828, 
    ShellNavigate19 = 829, 
    ShellNavigate20 = 830, 
    ShellNavigate21 = 831, 
    ShellNavigate22 = 832, 
    ShellNavigate23 = 833, 
    ShellNavigate24 = 834, 
    ShellNavigate25 = 835, 
    ShellNavigate26 = 836, 
    ShellNavigate27 = 837, 
    ShellNavigate28 = 838, 
    ShellNavigate29 = 839, 
    ShellNavigate30 = 840, 
    ShellNavigate31 = 841, 
    ShellNavigate32 = 842, 
    ShellNavigate33 = 843,   // must remain unused

    ShellWindowNavigate1 = 844, 
    ShellWindowNavigate2 = 845, 
    ShellWindowNavigate3 = 846, 
    ShellWindowNavigate4 = 847, 
    ShellWindowNavigate5 = 848, 
    ShellWindowNavigate6 = 849, 
    ShellWindowNavigate7 = 850, 
    ShellWindowNavigate8 = 851, 
    ShellWindowNavigate9 = 852, 
    ShellWindowNavigate10 = 853, 
    ShellWindowNavigate11 = 854, 
    ShellWindowNavigate12 = 855, 
    ShellWindowNavigate13 = 856, 
    ShellWindowNavigate14 = 857, 
    ShellWindowNavigate15 = 858, 
    ShellWindowNavigate16 = 859, 
    ShellWindowNavigate17 = 860, 
    ShellWindowNavigate18 = 861, 
    ShellWindowNavigate19 = 862, 
    ShellWindowNavigate20 = 863, 
    ShellWindowNavigate21 = 864, 
    ShellWindowNavigate22 = 865, 
    ShellWindowNavigate23 = 866, 
    ShellWindowNavigate24 = 867, 
    ShellWindowNavigate25 = 868, 
    ShellWindowNavigate26 = 869, 
    ShellWindowNavigate27 = 870, 
    ShellWindowNavigate28 = 871, 
    ShellWindowNavigate29 = 872, 
    ShellWindowNavigate30 = 873, 
    ShellWindowNavigate31 = 874, 
    ShellWindowNavigate32 = 875, 
    ShellWindowNavigate33 = 876,   // must remain unused

    // ObjectSearch cmds
    OBSDoFind = 877, 
    OBSMatchCase = 878, 
    OBSMatchSubString = 879, 
    OBSMatchWholeWord = 880, 
    OBSMatchPrefix = 881, 

    // build cmds
    BuildSln = 882, 
    RebuildSln = 883, 
    DeploySln = 884, 
    CleanSln = 885, 

    BuildSel = 886, 
    RebuildSel = 887, 
    DeploySel = 888, 
    CleanSel = 889, 

    CancelBuild = 890, 
    BatchBuildDlg = 891, 

    BuildCtx = 892, 
    RebuildCtx = 893, 
    DeployCtx = 894, 
    CleanCtx = 895, 

    QryManageIndexes = 896, 
    PrintDefault = 897, 		// quick print
    BrowseDoc = 898, 
    ShowStartPage = 899, 

    MRUFile1 = 900, 
    MRUFile2 = 901, 
    MRUFile3 = 902, 
    MRUFile4 = 903, 
    MRUFile5 = 904, 
    MRUFile6 = 905, 
    MRUFile7 = 906, 
    MRUFile8 = 907, 
    MRUFile9 = 908, 
    MRUFile10 = 909, 
    MRUFile11 = 910, 
    MRUFile12 = 911, 
    MRUFile13 = 912, 
    MRUFile14 = 913, 
    MRUFile15 = 914, 
    MRUFile16 = 915, 
    MRUFile17 = 916, 
    MRUFile18 = 917, 
    MRUFile19 = 918, 
    MRUFile20 = 919, 
    MRUFile21 = 920, 
    MRUFile22 = 921, 
    MRUFile23 = 922, 
    MRUFile24 = 923, 
    MRUFile25 = 924,   // note cmdidMRUFile25 is unused on purpose!

    //External Tools Context Menu Commands
    // continued at 1109
    ExtToolsCurPath = 925, 
    ExtToolsCurDir = 926, 
    ExtToolsCurFileName = 927, 
    ExtToolsCurExtension = 928, 
    ExtToolsProjDir = 929, 
    ExtToolsProjFileName = 930, 
    ExtToolsSlnDir = 931, 
    ExtToolsSlnFileName = 932, 


    // Object Browsing & ClassView cmds
    // Shared shell cmds (for accessing Object Browsing functionality)
    GotoDefn = 935, 
    GotoDecl = 936, 
    BrowseDefn = 937, 
    SyncClassView = 938, 
    ShowMembers = 939, 
    ShowBases = 940, 
    ShowDerived = 941, 
    ShowDefns = 942, 
    ShowRefs = 943, 
    ShowCallers = 944, 
    ShowCallees = 945, 

    AddClass = 946, 
    AddNestedClass = 947, 
    AddInterface = 948, 
    AddMethod = 949, 
    AddProperty = 950, 
    AddEvent = 951, 
    AddVariable = 952, 
    ImplementInterface = 953, 
    Override = 954, 
    AddFunction = 955, 
    AddConnectionPoint = 956, 
    AddIndexer = 957, 

    BuildOrder = 958, 
    //959 used above for cmdidSaveOptions

    // Object Browser Tool Specific cmds
    OBShowHidden = 960, 
    OBEnableGrouping = 961, 
    OBSetGroupingCriteria = 962, 
    OBBack = 963, 
    OBForward = 964, 
    OBShowPackages = 965, 
    OBSearchCombo = 966, 
    OBSearchOptWholeWord = 967,  
    OBSearchOptSubstring = 968,  
    OBSearchOptPrefix = 969,  
    OBSearchOptCaseSensitive = 970, 

    // ClassView Tool Specific cmds
    CVGroupingNone = 971, 
    CVGroupingSortOnly = 972, 
    CVGroupingGrouped = 973, 
    CVShowPackages = 974, 
    CVNewFolder = 975, 
    CVGroupingSortAccess = 976, 

    ObjectSearch = 977, 
    ObjectSearchResults = 978, 

    // Further Obj Browsing cmds at 1095

    // build cascade menus
    Build1 = 979, 
    Build2 = 980, 
    Build3 = 981, 
    Build4 = 982, 
    Build5 = 983, 
    Build6 = 984, 
    Build7 = 985, 
    Build8 = 986, 
    Build9 = 987, 
    BuildLast = 988, 

    Rebuild1 = 989, 
    Rebuild2 = 990, 
    Rebuild3 = 991, 
    Rebuild4 = 992, 
    Rebuild5 = 993, 
    Rebuild6 = 994, 
    Rebuild7 = 995, 
    Rebuild8 = 996, 
    Rebuild9 = 997, 
    RebuildLast = 998, 

    Clean1 = 999, 
    Clean2 = 1000, 
    Clean3 = 1001, 
    Clean4 = 1002, 
    Clean5 = 1003, 
    Clean6 = 1004, 
    Clean7 = 1005, 
    Clean8 = 1006, 
    Clean9 = 1007, 
    CleanLast = 1008, 

    Deploy1 = 1009, 
    Deploy2 = 1010, 
    Deploy3 = 1011, 
    Deploy4 = 1012, 
    Deploy5 = 1013, 
    Deploy6 = 1014, 
    Deploy7 = 1015, 
    Deploy8 = 1016, 
    Deploy9 = 1017, 
    DeployLast = 1018, 

    BuildProjPicker = 1019, 
    RebuildProjPicker = 1020, 
    CleanProjPicker = 1021, 
    DeployProjPicker = 1022, 
    ResourceView = 1023, 

    ShowHomePage = 1024, 
    EditMenuIDs = 1025, 

    LineBreak = 1026, 
    CPPIdentifier = 1027, 
    QuotedString = 1028, 
    SpaceOrTab = 1029, 
    Integer = 1030, 
    //unused 1031-1035

    CustomizeToolbars = 1036, 
    MoveToTop = 1037, 
    WindowHelp = 1038, 

    ViewPopup = 1039, 
    CheckMnemonics = 1040, 

    PRSortAlphabeticaly = 1041, 
    PRSortByCategory = 1042, 

    ViewNextTab = 1043, 

    CheckForUpdates = 1044, 

    Browser1 = 1045, 
    Browser2 = 1046, 
    Browser3 = 1047, 
    Browser4 = 1048, 
    Browser5 = 1049, 
    Browser6 = 1050, 
    Browser7 = 1051, 
    Browser8 = 1052, 
    Browser9 = 1053, 
    Browser10 = 1054, 
    Browser11 = 1055,  //note unused on purpose to end list

    OpenDropDownOpen = 1058, 
    OpenDropDownOpenWith = 1059, 

    ToolsDebugProcesses = 1060, 

    PaneNextSubPane = 1062, 
    PanePrevSubPane = 1063, 

    MoveFileToProject1 = 1070, 
    MoveFileToProject2 = 1071, 
    MoveFileToProject3 = 1072, 
    MoveFileToProject4 = 1073, 
    MoveFileToProject5 = 1074, 
    MoveFileToProject6 = 1075, 
    MoveFileToProject7 = 1076, 
    MoveFileToProject8 = 1077, 
    MoveFileToProject9 = 1078, 
    MoveFileToProjectLast = 1079,  // unused in order to end list
    MoveFileToProjectPick = 1081, 

    DefineSubset = 1095, 
    SubsetCombo = 1096, 
    SubsetGetList = 1097, 
    OBSortObjectsAlpha = 1098, 
    OBSortObjectsType = 1099, 
    OBSortObjectsAccess = 1100, 
    OBGroupObjectsType = 1101, 
    OBGroupObjectsAccess = 1102, 
    OBSortMembersAlpha = 1103, 
    OBSortMembersType = 1104, 
    OBSortMembersAccess = 1105, 

    PopBrowseContext = 1106, 
    GotoRef = 1107, 
    OBSLookInReferences = 1108, 

    ExtToolsTargetPath = 1109, 
    ExtToolsTargetDir = 1110, 
    ExtToolsTargetFileName = 1111, 
    ExtToolsTargetExtension = 1112, 
    ExtToolsCurLine = 1113, 
    ExtToolsCurCol = 1114, 
    ExtToolsCurText = 1115, 

    BrowseNext = 1116, 
    BrowsePrev = 1117, 
    BrowseUnload = 1118, 
    QuickObjectSearch = 1119, 
    ExpandAll = 1120, 

    StandardMax = 1500, 

    ///////////////////////////////////////////
    //
    // cmdidStandardMax is now thought to be
    // obsolete. Any new shell commands should
    // be added to the end of StandardCommandSet2K
    // which appears below.
    //
    // If you are not adding shell commands,
    // you shouldn't be doing it in this file! 
    //
    ///////////////////////////////////////////


    FormsFirst = 0x00006000, 

    FormsLast = 0x00006FFF, 

    VBEFirst = 0x00008000,    


    Zoom200 = 0x00008002, 
    Zoom150 = 0x00008003, 
    Zoom100 = 0x00008004, 
    Zoom75 = 0x00008005, 
    Zoom50 = 0x00008006, 
    Zoom25 = 0x00008007, 
    Zoom10 = 0x00008010, 


    VBELast = 0x00009FFF,  
                                         
    SterlingFirst = 0x0000A000,                                     
    SterlingLast = 0x0000BFFF,                                      

    uieventidFirst = 0xC000, 
    uieventidSelectRegion = 0xC001, 
    uieventidDrop = 0xC002, 
    uieventidLast = 0xDFFF, 

  }

  // VsConstants.guidStandardCommandSet9
  public enum VsCommands2K {
    TYPECHAR = 1, 
    BACKSPACE = 2, 
    RETURN = 3, 
    TAB = 4,  // test
    BACKTAB = 5, 
    DELETE = 6, 
    LEFT = 7, 
    LEFT_EXT = 8, 
    RIGHT = 9, 
    RIGHT_EXT = 10, 
    UP = 11, 
    UP_EXT = 12, 
    DOWN = 13, 
    DOWN_EXT = 14, 
    HOME = 15, 
    HOME_EXT = 16, 
    END = 17, 
    END_EXT = 18, 
    BOL = 19, 
    BOL_EXT = 20, 
    FIRSTCHAR = 21, 
    FIRSTCHAR_EXT = 22, 
    EOL = 23, 
    EOL_EXT = 24, 
    LASTCHAR = 25, 
    LASTCHAR_EXT = 26, 
    PAGEUP = 27, 
    PAGEUP_EXT = 28, 
    PAGEDN = 29, 
    PAGEDN_EXT = 30, 
    TOPLINE = 31, 
    TOPLINE_EXT = 32, 
    BOTTOMLINE = 33, 
    BOTTOMLINE_EXT = 34, 
    SCROLLUP = 35, 
    SCROLLDN = 36, 
    SCROLLPAGEUP = 37, 
    SCROLLPAGEDN = 38, 
    SCROLLLEFT = 39, 
    SCROLLRIGHT = 40, 
    SCROLLBOTTOM = 41, 
    SCROLLCENTER = 42, 
    SCROLLTOP = 43, 
    SELECTALL = 44, 
    SELTABIFY = 45, 
    SELUNTABIFY = 46, 
    SELLOWCASE = 47, 
    SELUPCASE = 48, 
    SELTOGGLECASE = 49, 
    SELTITLECASE = 50, 
    SELSWAPANCHOR = 51, 
    GOTOLINE = 52, 
    GOTOBRACE = 53, 
    GOTOBRACE_EXT = 54, 
    GOBACK = 55, 
    SELECTMODE = 56, 
    TOGGLE_OVERTYPE_MODE = 57, 
    CUT = 58, 
    COPY = 59, 
    PASTE = 60, 
    CUTLINE = 61, 
    DELETELINE = 62, 
    DELETEBLANKLINES = 63, 
    DELETEWHITESPACE = 64, 
    DELETETOEOL = 65, 
    DELETETOBOL = 66, 
    OPENLINEABOVE = 67, 
    OPENLINEBELOW = 68, 
    INDENT = 69, 
    UNINDENT = 70, 
    UNDO = 71, 
    UNDONOMOVE = 72, 
    REDO = 73, 
    REDONOMOVE = 74, 
    DELETEALLTEMPBOOKMARKS = 75, 
    TOGGLETEMPBOOKMARK = 76, 
    GOTONEXTBOOKMARK = 77, 
    GOTOPREVBOOKMARK = 78, 
    FIND = 79, 
    REPLACE = 80, 
    REPLACE_ALL = 81, 
    FINDNEXT = 82, 
    FINDNEXTWORD = 83, 
    FINDPREV = 84, 
    FINDPREVWORD = 85, 
    FINDAGAIN = 86, 
    TRANSPOSECHAR = 87, 
    TRANSPOSEWORD = 88, 
    TRANSPOSELINE = 89, 
    SELECTCURRENTWORD = 90, 
    DELETEWORDRIGHT = 91, 
    DELETEWORDLEFT = 92, 
    WORDPREV = 93, 
    WORDPREV_EXT = 94, 
    WORDNEXT = 96, 
    WORDNEXT_EXT = 97, 
    COMMENTBLOCK = 98, 
    UNCOMMENTBLOCK = 99, 
    SETREPEATCOUNT = 100, 
    WIDGETMARGIN_LBTNDOWN = 101, 
    SHOWCONTEXTMENU = 102, 
    CANCEL = 103, 
    PARAMINFO = 104, 
    TOGGLEVISSPACE = 105, 
    TOGGLECARETPASTEPOS = 106, 
    COMPLETEWORD = 107, 
    SHOWMEMBERLIST = 108, 
    FIRSTNONWHITEPREV = 109, 
    FIRSTNONWHITENEXT = 110, 
    HELPKEYWORD = 111, 
    FORMATSELECTION = 112, 
    OPENURL = 113,      
    INSERTFILE = 114, 
    TOGGLESHORTCUT = 115, 
    QUICKINFO = 116, 
    LEFT_EXT_COL = 117, 
    RIGHT_EXT_COL = 118, 
    UP_EXT_COL = 119, 
    DOWN_EXT_COL = 120, 
    TOGGLEWORDWRAP = 121, 
    ISEARCH = 122, 
    ISEARCHBACK = 123, 
    BOL_EXT_COL = 124, 
    EOL_EXT_COL = 125, 
    WORDPREV_EXT_COL = 126, 
    WORDNEXT_EXT_COL = 127, 
    OUTLN_HIDE_SELECTION = 128, 
    OUTLN_TOGGLE_CURRENT = 129, 
    OUTLN_TOGGLE_ALL = 130, 
    OUTLN_STOP_HIDING_ALL = 131, 
    OUTLN_STOP_HIDING_CURRENT = 132, 
    OUTLN_COLLAPSE_TO_DEF = 133, 
    DOUBLECLICK = 134, 
    EXTERNALLY_HANDLED_WIDGET_CLICK = 135, 
    COMMENT_BLOCK = 136, 
    UNCOMMENT_BLOCK = 137, 
    OPENFILE = 138, 
    NAVIGATETOURL = 139, 

    // For editor internal use only
    HANDLEIMEMESSAGE = 140, 

    SELTOGOBACK = 141, 
    COMPLETION_HIDE_ADVANCED = 142, 

    FORMATDOCUMENT = 143, 
    OUTLN_START_AUTOHIDING = 144, 

    // Last Standard Editor Command (+1)
    FINAL = 145, 

    ///////////////////////////////////////////////////////////////
    // Some new commands created during CTC file rationalisation
    ///////////////////////////////////////////////////////////////
    STOP = 220, 
    REVERSECANCEL = 221, 
    SLNREFRESH = 222, 
    SAVECOPYOFITEMAS = 223, 
    //
    // Shareable commands originating in the HTML editor
    //
    NEWELEMENT = 224, 
    NEWATTRIBUTE = 225, 
    NEWCOMPLEXTYPE = 226, 
    NEWSIMPLETYPE = 227, 
    NEWGROUP = 228, 
    NEWATTRIBUTEGROUP = 229, 
    NEWKEY = 230, 
    NEWRELATION = 231, 
    EDITKEY = 232, 
    EDITRELATION = 233, 
    MAKETYPEGLOBAL = 234, 
    PREVIEWDATASET = 235, 
    GENERATEDATASET = 236, 
    CREATESCHEMA = 237, 
    LAYOUTINDENT = 238, 
    LAYOUTUNINDENT = 239, 
    REMOVEHANDLER = 240, 
    EDITHANDLER = 241, 
    ADDHANDLER = 242, 
    STYLE = 243, 
    STYLEGETLIST = 244, 
    FONTSTYLE = 245, 
    FONTSTYLEGETLIST = 246, 
    PASTEASHTML = 247, 
    VIEWBORDERS = 248, 
    VIEWDETAILS = 249, 
    EXPANDCONTROLS = 250, 
    COLLAPSECONTROLS = 251, 
    SHOWSCRIPTONLY = 252, 
    INSERTTABLE = 253, 
    INSERTCOLLEFT = 254, 
    INSERTCOLRIGHT = 255, 
    INSERTROWABOVE = 256, 
    INSERTROWBELOW = 257, 
    DELETETABLE = 258, 
    DELETECOLS = 259, 
    DELETEROWS = 260, 
    SELECTTABLE = 261, 
    SELECTTABLECOL = 262, 
    SELECTTABLEROW = 263, 
    SELECTTABLECELL = 264, 
    MERGECELLS = 265, 
    SPLITCELL = 266, 
    INSERTCELL = 267, 
    DELETECELLS = 268, 
    SEAMLESSFRAME = 269, 
    VIEWFRAME = 270, 
    DELETEFRAME = 271, 
    SETFRAMESOURCE = 272, 
    NEWLEFTFRAME = 273, 
    NEWRIGHTFRAME = 274, 
    NEWTOPFRAME = 275, 
    NEWBOTTOMFRAME = 276, 
    SHOWGRID = 277, 
    SNAPTOGRID = 278, 
    BOOKMARK = 279, 
    HYPERLINK = 280, 
    IMAGE = 281, 
    INSERTFORM = 282, 
    INSERTSPAN = 283, 
    DIV = 284, 
    HTMLCLIENTSCRIPTBLOCK = 285, 
    HTMLSERVERSCRIPTBLOCK = 286, 
    BULLETEDLIST = 287, 
    NUMBEREDLIST = 288, 
    EDITSCRIPT = 289, 
    EDITCODEBEHIND = 290, 
    DOCOUTLINEHTML = 291, 
    DOCOUTLINESCRIPT = 292, 
    RUNATSERVER = 293, 
    WEBFORMSVERBS = 294, 
    WEBFORMSTEMPLATES = 295, 
    ENDTEMPLATE = 296, 
    EDITDEFAULTEVENT = 297, 
    SUPERSCRIPT = 298, 
    SUBSCRIPT = 299, 
    EDITSTYLE = 300, 
    ADDIMAGEHEIGHTWIDTH = 301, 
    REMOVEIMAGEHEIGHTWIDTH = 302, 
    LOCKELEMENT = 303, 
    VIEWSTYLEORGANIZER = 304, 
    ECMD_AUTOCLOSEOVERRIDE = 305, 
    NEWANY = 306, 
    NEWANYATTRIBUTE = 307, 
    DELETEKEY = 308, 
    AUTOARRANGE = 309, 
    VALIDATESCHEMA = 310, 
    NEWFACET = 311, 
    VALIDATEXMLDATA = 312, 
    DOCOUTLINETOGGLE = 313, 
    VALIDATEHTMLDATA = 314, 
    VIEWXMLSCHEMAOVERVIEW = 315, 
    //
    // Shareable commands originating in the VC project
    //
    COMPILE = 350, 
    //
    PROJSETTINGS = 352, 
    LINKONLY = 353, 
    //
    REMOVE = 355, 
    PROJSTARTDEBUG = 356, 
    PROJSTEPINTO = 357, 
    //
    //
    UPDATEWEBREF = 360, 
    //
    ADDRESOURCE = 362, 
    WEBDEPLOY = 363, 
    //
    // Shareable commands originating in the VB and VBA projects
    // Note that there are two versions of each command. One
    // version is originally from the main (project) menu and the
    // other version from a cascading "Add" context menu. The main
    // difference between the two commands is that the main menu
    // version starts with the text "Add" whereas this is not
    // present on the context menu version.
    //
    ADDHTMLPAGE = 400, 
    ADDHTMLPAGECTX = 401, 
    ADDMODULE = 402, 
    ADDMODULECTX = 403, 
    // unused 404
    // unused 405
    ADDWFCFORM = 406, 
    // unused 407
    // unused 408
    // unused 409
    ADDWEBFORM = 410, 
    // unused 411
    ADDUSERCONTROL = 412, 
    // unused 413 to 425
    ADDDHTMLPAGE = 426, 
    // unused 427 to 431
    ADDIMAGEGENERATOR = 432, 
    // unused 433
    ADDINHERWFCFORM = 434, 
    // unused 435
    ADDINHERCONTROL = 436, 
    // unused 437
    ADDWEBUSERCONTROL = 438, 
    BUILDANDBROWSE = 439, 
    // unused 440
    // unused 441
    ADDTBXCOMPONENT = 442, 
    // unused 443
    ADDWEBSERVICE = 444, 
    // unused 445
    //
    // Shareable commands originating in the VFP project
    //
    ADDVFPPAGE = 500, 
    SETBREAKPOINT = 501, 
    //
    // Shareable commands originating in the HELP WORKSHOP project
    //
    SHOWALLFILES = 600, 
    ADDTOPROJECT = 601, 
    ADDBLANKNODE = 602, 
    ADDNODEFROMFILE = 603, 
    CHANGEURLFROMFILE = 604, 
    EDITTOPIC = 605, 
    EDITTITLE = 606, 
    MOVENODEUP = 607, 
    MOVENODEDOWN = 608, 
    MOVENODELEFT = 609, 
    MOVENODERIGHT = 610, 
    //
    // Shareable commands originating in the Deploy project
    //
    // Note there are two groups of deploy project commands.
    // The first group of deploy commands.
    ADDOUTPUT = 700, 
    ADDFILE = 701, 
    MERGEMODULE = 702, 
    ADDCOMPONENTS = 703, 
    LAUNCHINSTALLER = 704, 
    LAUNCHUNINSTALL = 705, 
    LAUNCHORCA = 706, 
    FILESYSTEMEDITOR = 707, 
    REGISTRYEDITOR = 708, 
    FILETYPESEDITOR = 709, 
    USERINTERFACEEDITOR = 710, 
    CUSTOMACTIONSEDITOR = 711, 
    LAUNCHCONDITIONSEDITOR = 712, 
    EDITOR = 713, 
    EXCLUDE = 714, 
    REFRESHDEPENDENCIES = 715, 
    VIEWOUTPUTS = 716, 
    VIEWDEPENDENCIES = 717, 
    VIEWFILTER = 718, 

    //
    // The Second group of deploy commands.
    // Note that there is a special sub-group in which the relative 
    // positions are important (see below)
    //
    KEY = 750, 
    STRING = 751, 
    BINARY = 752, 
    DWORD = 753, 
    KEYSOLO = 754, 
    IMPORT = 755, 
    FOLDER = 756, 
    PROJECTOUTPUT = 757, 
    FILE = 758, 
    ADDMERGEMODULES = 759, 
    CREATESHORTCUT = 760, 
    LARGEICONS = 761, 
    SMALLICONS = 762, 
    LIST = 763, 
    DETAILS = 764, 
    ADDFILETYPE = 765, 
    ADDACTION = 766, 
    SETASDEFAULT = 767, 
    MOVEUP = 768, 
    MOVEDOWN = 769, 
    ADDDIALOG = 770, 
    IMPORTDIALOG = 771, 
    ADDFILESEARCH = 772, 
    ADDREGISTRYSEARCH = 773, 
    ADDCOMPONENTSEARCH = 774, 
    ADDLAUNCHCONDITION = 775, 
    ADDCUSTOMACTION = 776, 
    OUTPUTS = 777, 
    DEPENDENCIES = 778, 
    FILTER = 779, 
    COMPONENTS = 780, 
    ENVSTRING = 781, 
    CREATEEMPTYSHORTCUT = 782, 
    ADDFILECONDITION = 783, 
    ADDREGISTRYCONDITION = 784, 
    ADDCOMPONENTCONDITION = 785, 
    ADDURTCONDITION = 786, 
    ADDIISCONDITION = 787, 

    //
    // The relative positions of the commands within the following deploy
    // subgroup must remain unaltered, although the group as a whole may
    // be repositioned. Note that the first and last elements are special
    // boundary elements.
    SPECIALFOLDERBASE = 800, 
    USERSAPPLICATIONDATAFOLDER = 800, 
    COMMONFILESFOLDER = 801, 
    CUSTOMFOLDER = 802, 
    USERSDESKTOP = 803, 
    USERSFAVORITESFOLDER = 804, 
    FONTSFOLDER = 805, 
    GLOBALASSEMBLYCACHEFOLDER = 806, 
    MODULERETARGETABLEFOLDER = 807, 
    USERSPERSONALDATAFOLDER = 808, 
    PROGRAMFILESFOLDER = 809, 
    USERSPROGRAMSMENU = 810, 
    USERSSENDTOMENU = 811, 
    SHAREDCOMPONENTSFOLDER = 812, 
    USERSSTARTMENU = 813, 
    USERSSTARTUPFOLDER = 814, 
    SYSTEMFOLDER = 815, 
    APPLICATIONFOLDER = 816, 
    USERSTEMPLATEFOLDER = 817, 
    WEBCUSTOMFOLDER = 818, 
    WINDOWSFOLDER = 819, 
    SPECIALFOLDERLAST = 819, 
    // End of deploy sub-group
    //
    // Shareable commands originating in the Visual Studio Analyzer project
    //
    EXPORTEVENTS = 900, 
    IMPORTEVENTS = 901, 
    VIEWEVENT = 902, 
    VIEWEVENTLIST = 903, 
    VIEWCHART = 904, 
    VIEWMACHINEDIAGRAM = 905, 
    VIEWPROCESSDIAGRAM = 906, 
    VIEWSOURCEDIAGRAM = 907, 
    VIEWSTRUCTUREDIAGRAM = 908, 
    VIEWTIMELINE = 909, 
    VIEWSUMMARY = 910, 
    APPLYFILTER = 911, 
    CLEARFILTER = 912, 
    STARTRECORDING = 913, 
    STOPRECORDING = 914, 
    PAUSERECORDING = 915, 
    ACTIVATEFILTER = 916, 
    SHOWFIRSTEVENT = 917, 
    SHOWPREVIOUSEVENT = 918, 
    SHOWNEXTEVENT = 919, 
    SHOWLASTEVENT = 920, 
    REPLAYEVENTS = 921, 
    STOPREPLAY = 922, 
    INCREASEPLAYBACKSPEED = 923, 
    DECREASEPLAYBACKSPEED = 924, 
    ADDMACHINE = 925, 
    ADDREMOVECOLUMNS = 926, 
    SORTCOLUMNS = 927, 
    SAVECOLUMNSETTINGS = 928, 
    RESETCOLUMNSETTINGS = 929, 
    SIZECOLUMNSTOFIT = 930, 
    AUTOSELECT = 931, 
    AUTOFILTER = 932, 
    AUTOPLAYTRACK = 933, 
    GOTOEVENT = 934, 
    ZOOMTOFIT = 935, 
    ADDGRAPH = 936, 
    REMOVEGRAPH = 937, 
    CONNECTMACHINE = 938, 
    DISCONNECTMACHINE = 939, 
    EXPANDSELECTION = 940, 
    COLLAPSESELECTION = 941, 
    ADDFILTER = 942, 
    ADDPREDEFINED0 = 943, 
    ADDPREDEFINED1 = 944, 
    ADDPREDEFINED2 = 945, 
    ADDPREDEFINED3 = 946, 
    ADDPREDEFINED4 = 947, 
    ADDPREDEFINED5 = 948, 
    ADDPREDEFINED6 = 949, 
    ADDPREDEFINED7 = 950, 
    ADDPREDEFINED8 = 951, 
    TIMELINESIZETOFIT = 952, 

    //
    // Shareable commands originating with Crystal Reports
    //
    FIELDVIEW = 1000, 
    SELECTEXPERT = 1001, 
    TOPNEXPERT = 1002, 
    SORTORDER = 1003, 
    PROPPAGE = 1004, 
    HELP = 1005, 
    SAVEREPORT = 1006, 
    INSERTSUMMARY = 1007, 
    INSERTGROUP = 1008, 
    INSERTSUBREPORT = 1009, 
    INSERTCHART = 1010, 
    INSERTPICTURE = 1011, 
    //
    // Shareable commands from the common project area (DirPrj)
    //
    SETASSTARTPAGE = 1100, 
    RECALCULATELINKS = 1101, 
    WEBPERMISSIONS = 1102, 
    COMPARETOMASTER = 1103, 
    WORKOFFLINE = 1104, 
    SYNCHRONIZEFOLDER = 1105, 
    SYNCHRONIZEALLFOLDERS = 1106, 
    COPYPROJECT = 1107, 
    IMPORTFILEFROMWEB = 1108, 
    INCLUDEINPROJECT = 1109, 
    EXCLUDEFROMPROJECT = 1110, 
    BROKENLINKSREPORT = 1111, 
    ADDPROJECTOUTPUTS = 1112, 
    ADDREFERENCE = 1113, 
    ADDWEBREFERENCE = 1114, 
    ADDWEBREFERENCECTX = 1115, 
    UPDATEWEBREFERENCE = 1116, 
    RUNCUSTOMTOOL = 1117,  
    //
    // Shareable commands for right drag operations
    //
    DRAG_MOVE = 1140, 
    DRAG_COPY = 1141, 
    DRAG_CANCEL = 1142, 

    //
    // Shareable commands from the VC resource editor
    //
    TESTDIALOG = 1200, 
    SPACEACROSS = 1201, 
    SPACEDOWN = 1202, 
    TOGGLEGRID = 1203, 
    TOGGLEGUIDES = 1204, 
    SIZETOTEXT = 1205, 
    CENTERVERT = 1206, 
    CENTERHORZ = 1207, 
    FLIPDIALOG = 1208, 
    SETTABORDER = 1209, 
    BUTTONRIGHT = 1210, 
    BUTTONBOTTOM = 1211, 
    AUTOLAYOUTGROW = 1212, 
    AUTOLAYOUTNORESIZE = 1213, 
    AUTOLAYOUTOPTIMIZE = 1214, 
    GUIDESETTINGS = 1215, 
    RESOURCEINCLUDES = 1216, 
    RESOURCESYMBOLS = 1217, 
    OPENBINARY = 1218, 
    RESOURCEOPEN = 1219, 
    RESOURCENEW = 1220, 
    RESOURCENEWCOPY = 1221, 
    INSERT = 1222, 
    EXPORT = 1223, 
    CTLMOVELEFT = 1224, 
    CTLMOVEDOWN = 1225, 
    CTLMOVERIGHT = 1226, 
    CTLMOVEUP = 1227, 
    CTLSIZEDOWN = 1228, 
    CTLSIZEUP = 1229, 
    CTLSIZELEFT = 1230, 
    CTLSIZERIGHT = 1231, 
    NEWACCELERATOR = 1232, 
    CAPTUREKEYSTROKE = 1233, 
    INSERTACTIVEXCTL = 1234, 
    INVERTCOLORS = 1235, 
    FLIPHORIZONTAL = 1236, 
    FLIPVERTICAL = 1237, 
    ROTATE90 = 1238, 
    SHOWCOLORSWINDOW = 1239, 
    NEWSTRING = 1240, 
    NEWINFOBLOCK = 1241, 
    DELETEINFOBLOCK = 1242, 
    ADJUSTCOLORS = 1243, 
    LOADPALETTE = 1244, 
    SAVEPALETTE = 1245, 
    CHECKMNEMONICS = 1246, 
    DRAWOPAQUE = 1247, 
    TOOLBAREDITOR = 1248, 
    GRIDSETTINGS = 1249, 
    NEWDEVICEIMAGE = 1250, 
    OPENDEVICEIMAGE = 1251, 
    DELETEDEVICEIMAGE = 1252, 
    VIEWASPOPUP = 1253, 
    CHECKMENUMNEMONICS = 1254, 
    SHOWIMAGEGRID = 1255, 
    SHOWTILEGRID = 1256, 
    MAGNIFY = 1257, 
    ResProps = 1258, 
    //
    // Shareable commands from the VC resource editor (Image editor toolbar)
    //
    PICKRECTANGLE = 1300, 
    PICKREGION = 1301, 
    PICKCOLOR = 1302, 
    ERASERTOOL = 1303, 
    FILLTOOL = 1304, 
    PENCILTOOL = 1305, 
    BRUSHTOOL = 1306, 
    AIRBRUSHTOOL = 1307, 
    LINETOOL = 1308, 
    CURVETOOL = 1309, 
    TEXTTOOL = 1310, 
    RECTTOOL = 1311, 
    OUTLINERECTTOOL = 1312, 
    FILLEDRECTTOOL = 1313, 
    ROUNDRECTTOOL = 1314, 
    OUTLINEROUNDRECTTOOL = 1315, 
    FILLEDROUNDRECTTOOL = 1316, 
    ELLIPSETOOL = 1317, 
    OUTLINEELLIPSETOOL = 1318, 
    FILLEDELLIPSETOOL = 1319, 
    SETHOTSPOT = 1320, 
    ZOOMTOOL = 1321, 
    ZOOM1X = 1322, 
    ZOOM2X = 1323, 
    ZOOM6X = 1324, 
    ZOOM8X = 1325, 
    TRANSPARENTBCKGRND = 1326, 
    OPAQUEBCKGRND = 1327, 
    //---------------------------------------------------
    // The commands ECMD_ERASERSMALL thru ECMD_LINELARGER
    // must be left in the same order for the use of the
    // Resource Editor - They may however be relocated as
    // a complete block
    //---------------------------------------------------
    ERASERSMALL = 1328, 
    ERASERMEDIUM = 1329, 
    ERASERLARGE = 1330, 
    ERASERLARGER = 1331, 
    CIRCLELARGE = 1332, 
    CIRCLEMEDIUM = 1333, 
    CIRCLESMALL = 1334, 
    SQUARELARGE = 1335, 
    SQUAREMEDIUM = 1336, 
    SQUARESMALL = 1337, 
    LEFTDIAGLARGE = 1338, 
    LEFTDIAGMEDIUM = 1339, 
    LEFTDIAGSMALL = 1340, 
    RIGHTDIAGLARGE = 1341, 
    RIGHTDIAGMEDIUM = 1342, 
    RIGHTDIAGSMALL = 1343, 
    SPLASHSMALL = 1344, 
    SPLASHMEDIUM = 1345, 
    SPLASHLARGE = 1346, 
    LINESMALLER = 1347, 
    LINESMALL = 1348, 
    LINEMEDIUM = 1349, 
    LINELARGE = 1350, 
    LINELARGER = 1351, 
    LARGERBRUSH = 1352, 
    LARGEBRUSH = 1353, 
    STDBRUSH = 1354, 
    SMALLBRUSH = 1355, 
    SMALLERBRUSH = 1356, 
    ZOOMIN = 1357, 
    ZOOMOUT = 1358, 
    PREVCOLOR = 1359, 
    PREVECOLOR = 1360, 
    NEXTCOLOR = 1361, 
    NEXTECOLOR = 1362, 
    IMG_OPTIONS = 1363, 

    //---------------------------------------------------

    //
    // Shareable commands from WINFORMS
    //
    CANCELDRAG = 1500, 
    DEFAULTACTION = 1501, 
    CTLMOVEUPGRID = 1502, 
    CTLMOVEDOWNGRID = 1503, 
    CTLMOVELEFTGRID = 1504, 
    CTLMOVERIGHTGRID = 1505, 
    CTLSIZERIGHTGRID = 1506, 
    CTLSIZEUPGRID = 1507, 
    CTLSIZELEFTGRID = 1508, 
    CTLSIZEDOWNGRID = 1509, 
    NEXTCTL = 1510, 
    PREVCTL = 1511, 

    // this is coming in with the VS2K guid?
    QUICKOBJECTSEARCH = 1119, 
  }
}
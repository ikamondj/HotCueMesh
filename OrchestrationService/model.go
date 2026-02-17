package main

type HotCueType string
type CueColor uint16
type CueMatchType string
type AppID string
type ActionType string
type ArgName string

const (
	Hot_Cue         HotCueType = "Hot_Cue"
	Action          HotCueType = "Action"
	BeatGrid_Anchor HotCueType = "BeatGrid_Anchor"
	Load_Point      HotCueType = "Load_Point"
	Saved_Loop      HotCueType = "Saved_Loop"
	Remix_Point     HotCueType = "Remix_Point"
	Automix_Point   HotCueType = "Automix_Point"

	Invisible CueColor = 1
	DarkGrey  CueColor = 2
	LightGrey CueColor = 4
	White     CueColor = 8
	Burgundy  CueColor = 16
	Apricot   CueColor = 32
	Red       CueColor = 64
	Orange    CueColor = 128
	Yellow    CueColor = 256
	Eggshell  CueColor = 512
	Green     CueColor = 1024
	Cyan      CueColor = 2048
	Cobalt    CueColor = 4096
	Blue      CueColor = 8192
	Purple    CueColor = 16384
	Magenta   CueColor = 32768

	None       CueMatchType = "None"
	Exact      CueMatchType = "Exact"
	Contains   CueMatchType = "Contains"
	StartsWith CueMatchType = "StartsWith"
	EndsWith   CueMatchType = "EndsWith"
	Embedded   CueMatchType = "Embedded"

	OBS           AppID      = "OBS"
	SceneName     ArgName    = "sceneName"
	SourceName    ArgName    = "sourceName"
	FilterName    ArgName    = "filterName"
	DisplaySource ActionType = "DisplaySource" // Uses SceneName and SourceName
	HideSource    ActionType = "HideSource"    // Uses SceneName and SourceName
	ToggleSource  ActionType = "ToggleSource"  // Uses SceneName and SourceName
	EnableFilter  ActionType = "EnableFilter"  // Uses SourceName and FilterName
	DisableFilter ActionType = "DisableFilter" // Uses SourceName and FilterName
	ToggleFilter  ActionType = "ToggleFilter"  // Uses SourceName and FilterName
	ChangeToScene ActionType = "ChangeToScene" // Uses SceneName

	LightingController AppID      = "LightingController"
	LightPreset        ActionType = "SetLightPreset"
	RandPreset         ActionType = "SetRandomPreset"
	PresetValue        ArgName    = "presetValue"
	PresetList         ArgName    = "presetList"

	HttpEndpoint AppID      = "HttpEndpoint"
	TcpEndpoint  AppID      = "TcpEndpoint"
	UdpEndpoint  AppID      = "UdpEndpoint"
	OscEndpoint  AppID      = "OscEndpoint"
	SendRequest  ActionType = "SendRequest"
	TargetHost   ArgName    = "targetHost"
	Port         ArgName    = "port"
	Path         ArgName    = "path"
)

type TriggerAction struct {
	AppId      AppID              `json:"appId"`
	ActionType ActionType         `json:"actionType"`
	Args       map[ArgName]string `json:"args"`
}

type HotcueEvent struct {
	CueMatchType CueMatchType
	CueName      string
	CueColor     CueColor
	Deck         int
	HotCueType   HotCueType
}

type Trigger struct {
	HotCueType   map[HotCueType]bool `json:"hotCueType"`
	CueMatchType CueMatchType        `json:"cueMatchType"`
	CueColor     map[CueColor]bool   `json:"cueColor"`
	Decks        map[int]bool        `json:"decks"`
	CueName      string              `json:"cueName"`
	Actions      []TriggerAction     `json:"actions"`
}

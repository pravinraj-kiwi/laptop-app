# Configuration
Device configuration, specifically button/event mapping are retrieved by the client at runtime. Currently, the client checks for a new configuration every hour. This is configurable in the application's settings.

On first run, the client will generate a defaultconfiguration.json file at C:\Users\\[user]\AppData\Local\PursuitAlert with the default button configuration. Once the configuration is created, it will be used until a new configuration is retrieved from the Server. The Server is checked for a configuration immediately on startup as well, so if a department has a custom configuration, the default configuration will be replaced immediately.

### Polling
The polling process happens immediately on startup and then every 1 hour following. The configuration is retrieved via HTTP using the URL provided in the ConfigurationURL application setting.

The Server responds with the current configuration with each request. Once received, the client compares the retrieved configuration with the current configuration (using a custom .Equals override defined in PursuitAlert.Domain\Modes\Models\Mode.cs). If the configuration does not match, the existing configuration is replaced with the new configuration (defaultconfiguration.json is overwritten with the new configuration) and the new configuration is loaded into memory to be used immediately.

### Configuration Properties

#### Type
* **Description**: The type of payload to send (single-character payload, used in Mqtt messages, see the telemetryHandler.js file in the pa-serverless/pa-iot project to see how this value is used).
* **Default**: none

#### Animate
* **Description**: Determines whether or not the Pursuit Alert logo will be animated in the client. All active modes should be animated.
* **Default**: true
* **Values**:
  * "N" - Power On
  * "T" - Patrol
  * "D" - Drop Pin
  * "P" - Pursuit
  * "C" - Code 3
  * "L" - All Clear
  * "F" - Power Off
  * "R" - Critical Incident
  * "M" - Move Over

#### ButtonPosition
* **Description**: The button number this mode is attached to (1-based index). The order of the buttons go from right to left with the USB connection on the right-side of the device (e.g. the green button is button 1, the yellow is button 2, etc.). If a button position is not defined, the mode will not be mapped to the device.
* **Default**: none

#### ColorName
* **Description**: The color to be used for the Pursuit Alert logo in the client. 
* **Default**: none
* **Available Colors** (defined in PursuitAlert.Client\Resources\XAML\Colors.xaml):
  * EmergencyRed (235,30,39)
  * MoveOverYellow (255,212,0)
  * PursuitBlue (62,92,170)
  * CriticalGreen (0,148,56)

#### Message / DisplayName
* **Description**: The message that will be passed to the mobile application when the mode is activated.
* **Default**: none

#### Heading
* **Description**: The heading that will be shown in the client to the Officer
* **Default**: Active

#### Name
* **Description**: The name of the mode (should not contain spaces and should be unique, this is used to identify the mode in the client)
* **Default**: none

#### PayloadInterval
* **Description**: The interval at which the an MQTT message will be dispatched while the mode is active
* **Default**: none

#### PayloadKind
* **Description**: The kind of payload to be sent via MQTT
* **Default** none
* **Values**:
  * None
  * OneTime
  * Repeating

#### PlaySound
* **Description**: Whether the client should play a sound when the mode is activated and deactivated. Any active modes should play a sound when activated.
* **Default**: true

#### SendAllClearWhenEnded
* **Description**: Whether or not an AllClear message should be sent when the mode is deactivated. Note: the message will only be sent when *all* modes that require an AllClear message to be sent are deactivated.
* **Default**: false

#### TimeOutUntilMoveOverInSeconds
* **Description**: The number of seconds the vehicle is stationary with the mode activated until MoveOver mode is activated. Specify 0 to not enable MoveOver mode. (e.g. If set to 30 seconds, when a vehicle is in Pursuit Mode, but is then stationary for 30 seconds, the device will automatically deactivate Pursuit Mode and activate Move Over mode).
* **Default**: 0

#### IncludeBearing
* **Description**: Whether or not the vehicle's bearing should be included in the MQTT messages sent as a result of the mode
* **Default**: false

#### IncludeSpeed
* **Description**: Whether or not the vehicle's speed should be included in the the MQTT messages sent as a result of the mode
* **Default**: false

#### AssignableToButton
* **Description**: Whether or not this mode can be assigned to a button on the device. This is used for "stateful" modes, i.e. PowerOn that the Officer should not use a button to activate and should be automatic
* **Default**: false

#### Configurable
* **Description**: Whether the mode's settings can be changed
* **Default**: true

#### IncidentDealy
* **Description** The number of seconds to countdown after the mode is triggered until the mode is activated. Use this option to specify a mode that should only be activated with extreme discretion. This displays a countdown on the client for the specified number of seconds and allows the Officer time to cancel the mode before it is activated to mitigate false positives.
* **Default**: 0

### Default Configuration
`json
{
  "Modes": [
    {
      "type": "L",
      "animate": false,
      "buttonPosition": -1,
      "colorName": "LightBrush",
      "displayName": "All Clear",
      "heading": "All Clear",
      "name": "AllClearMode",
      "payloadInterval": -1,
      "payloadKind": "OneTime",
      "playSound": false,
      "sendAllClearWhenEnded": false,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": false,
      "includeSpeed": false,
      "assignableToButton": false,
      "configurable": false,
      "incidentDelay": 0
    },
    {
      "type": "C",
      "animate": true,
      "buttonPosition": 3,
      "colorName": "EmergencyRedBrush",
      "displayName": "Emergency",
      "heading": "Active",
      "name": "EmergencyMode",
      "payloadInterval": 5000,
      "payloadKind": "Repeating",
      "playSound": true,
      "sendAllClearWhenEnded": true,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": true,
      "includeSpeed": true,
      "assignableToButton": true,
      "configurable": true,
      "incidentDelay": 0
    },
    {
      "type": "R",
      "animate": true,
      "buttonPosition": 1,
      "colorName": "CriticalGreenBrush",
      "displayName": "Critical Incident",
      "heading": "Active",
      "name": "CriticalIncidentMode",
      "payloadInterval": 5000,
      "payloadKind": "Repeating",
      "playSound": true,
      "sendAllClearWhenEnded": true,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": false,
      "includeSpeed": false,
      "assignableToButton": true,
      "configurable": true,
      "incidentDelay": 5
    },
    {
      "type": "D",
      "animate": false,
      "buttonPosition": 5,
      "colorName": "LightBrush",
      "displayName": "Pin Dropped",
      "heading": "-",
      "name": "PinDrop",
      "payloadInterval": -1,
      "payloadKind": "OneTime",
      "playSound": true,
      "sendAllClearWhenEnded": false,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": false,
      "includeSpeed": false,
      "assignableToButton": true,
      "configurable": true,
      "incidentDelay": 0
    },
    {
      "type": "M",
      "animate": true,
      "buttonPosition": 2,
      "colorName": "MoveOverYellowBrush",
      "displayName": "Move Over",
      "heading": "Active",
      "name": "MoveOverMode",
      "payloadInterval": 10000,
      "payloadKind": "Repeating",
      "playSound": true,
      "sendAllClearWhenEnded": true,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": false,
      "includeSpeed": false,
      "assignableToButton": true,
      "configurable": true,
      "incidentDelay": 0
    },
    {
      "type": "T",
      "animate": false,
      "buttonPosition": -1,
      "colorName": "LightBrush",
      "displayName": "On Patrol",
      "heading": "Active",
      "name": "PatrolMode",
      "payloadInterval": 30000,
      "payloadKind": "Repeating",
      "playSound": false,
      "sendAllClearWhenEnded": false,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": true,
      "includeSpeed": true,
      "assignableToButton": false,
      "configurable": true,
      "incidentDelay": 0
    },
    {
      "type": "P",
      "animate": true,
      "buttonPosition": 4,
      "colorName": "PursuitBlueBrush",
      "displayName": "In Pursuit",
      "heading": "Active",
      "name": "PursuitMode",
      "payloadInterval": 5000,
      "payloadKind": "Repeating",
      "playSound": true,
      "sendAllClearWhenEnded": true,
      "timeOutUntilMoveOverInSeconds": 30,
      "includeBearing": true,
      "includeSpeed": true,
      "assignableToButton": true,
      "configurable": true,
      "incidentDelay": 0
    },
    {
      "type": "N",
      "animate": false,
      "buttonPosition": -1,
      "colorName": "LightBrush",
      "displayName": "Power On",
      "heading": "-",
      "name": "PowerOn",
      "payloadInterval": -1,
      "payloadKind": "OneTime",
      "playSound": false,
      "sendAllClearWhenEnded": false,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": false,
      "includeSpeed": false,
      "assignableToButton": false,
      "configurable": false,
      "incidentDelay": 0
    },
    {
      "type": "F",
      "animate": false,
      "buttonPosition": -1,
      "colorName": "LightBrush",
      "displayName": "Power Off",
      "heading": "-",
      "name": "PowerOff",
      "payloadInterval": -1,
      "payloadKind": "OneTime",
      "playSound": false,
      "sendAllClearWhenEnded": false,
      "timeOutUntilMoveOverInSeconds": -1,
      "includeBearing": false,
      "includeSpeed": false,
      "assignableToButton": false,
      "configurable": false,
      "incidentDelay": 0
    }
  ]
}
`
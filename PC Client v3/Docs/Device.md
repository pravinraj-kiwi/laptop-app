# Device
A Pursuit Alert device should be connected to a PC via USB. The device should also be connected to a GPS module. On startup, the application begins polling the registry for all connected devices that match the configured PID (Product ID) and VID (Vendor ID) in the application's settings. If a device is found matching the PID and VID, the client attempts to open the device for communication. 

If opening the device fails, the user will see an error message in the client that the device couldn't be connected.

### Data
Once the device is opened, an event handler is attached to the `SerialPort.DataReceived` event that processes all data the comes from the device. The payload is received in string form and is passed to the Payload Processing service at `PursuitAlert.Application\Device\Payload\Servicse\DevicePayloadService.cs` to determine what type of payload was received and how it should be further processed.

Once the payload type is identified and parsed, an event is sent to the rest of the system to indicate that a payload has been received, i.e. `DeviceSwitchStatusReceivedEvent` or `CoordinatePayloadReceivedEvent`.

### Communication
Communication with the device is handled via the `SerialPort` class in `System.IO.Ports`. 

**Important**: When sending a payload *to* the device, the payload string must be terminated with "\r\n" in order for the device accept the payload. If the terminator is not included, the device will simply ignore the payload.

### Serial Number
Before the client can communicate with the MQTT broker, it must have the device serial number. Currently, if the device has a configured serial number, it is returned with every button press. If the serial number is not configured on the device, no serial number will be included in the button press payload. See the section below on Button Presses to see how serial numbers are provided in a button status payload.

### Button Presses
When a button is pressed on the device, a payload is sent to the client defining which button has been activated. The payload uses a 0-based index for the button positions.

Example button press payload (button 1 has been activated in this example):
```
$SW,1,0,0,0,0,x2t33jx4
```

When a button activation is recognized, a `DeviceSwitchActivatedEvent` is dispatched.

#### Payload Anatomy
* `$SW,`: The switch status preamble. Used to distinguish the payload as a switch status payload.
* `1,0,0,0,0,`: Button statuses. Button statuses are indicated by a `1` (activated) or a `0` (deactivated).
* `x2t33jx4`: The device serial number

### GPS Payloads
The device sends the client a GPS payload ever second. When a GPS payload is received, it is parsed using [`SharpGIS.NmeaParser`](https://github.com/dotMorten/NmeaParser).

Example GPS payload:
```
$GNGGA,144322.00,3437.07243,N,08227.89799,W,1,06,5.94,304.9,M,-32.1,M,,*77
```

#### Payload Anatomy
* `$GNGGA,`: The GPS payload preamble. Used to distinguish the payload as a GPS status payload (a.k.a GGA Message ID)
* `144322.00,`: The timestamp of the payload (hhmmss.ss)
* `3437.07243,`: Latitude
* `N,`: North/South indicator
* `08227.89733,`: Longitude
* `W,`: East/West indicator
* `1,`: Quality indicator for position fix (1/2: Dead reckoning fix, 4: RTK fixed, 5: RTK float, 6: Dead reckoning fix/user limits exceeded, 0: No position fix)
* `06,`: Number of satellites used in range (0-12)
* `5.94,`: Horizontal Dilution of Precision
* `304.9,`: Altitude above mean sea level
* `M,`: Altitude units (M: meters, fixed field)
* `-32.1,`: Geoid separation (difference between ellipsoid and mean sea level)
* `M,`: Geoid separation units (M: meters, fixed field)
* `[not in example]`: Age of differential corrections (null when DGPS is not used, numeric value)
* `[not in example]`: ID of station providing differential corrections (null when DGPS is not used, numeric value)
* `*77`: Hexadecimal checksum of payload

### LEDs
When a button is pressed on the device and it's corresponding mode activated, the appropriate LED on the device is lit by sending the LED payload to the device:

`#LED,1,0,0,0,0,0,0\r\n`

Issuing this payload to the device will cause LED 1 to light.

The corresponding command to turn off LED 1 would be:

`#LED,0,0,0,0,0,0,0\r\n`

#### Payload Anatomy
* `#LED,`: The LED payload preamble. Used to distinguish the payload as an LED payload
* `1,0,0,0,0,0,0`: LED statuses. Statuses are indicated by a `1` (LED On) or a `0` (LED Off).
* `\r\n`: Line terminator. Required for all commands sent to the device

**Important**: When sending a payload *to* the device, the payload string must be terminated with "\r\n" in order for the device accept the payload. If the terminator is not included, the device will simply ignore the payload.

### Device Disconnection
The `DeviceMonitorService` creates an Win32 event handler at runtime for devices removed from the PC. When a device is removed from the PC, the `DeviceMonitorService` attempts to re-open a connection to the Pursuit Alert device, but if that connection fails, a `DeviceDeisconnectedEvent` is fired. And the monitor for a Pursuit Alert device being connected to the system is reengaged.
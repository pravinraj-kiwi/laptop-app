# Message format
Sample message:
```json
{"T":"P","U":"2021-02-01T19:42:54.8694683Z","L":34.813505,"O":-82.270584333333332,"S":0,"B":0,"serial":"fsy91itz"}
```
Schema definition:
### T
The event type. Will always be represented by 1 or 2 characters. 2 character event types are "end" event types to signal that an event has ended.
##### Valid values
* **[DEPRECATED]** **N**: (Power On) the device has just come online. This was used in the Zipit system, but has since been removed. The initial PATROL signal that is sent up by the deivce is interpreted as the signal that the device has powered on.
* **T**: (Patrol) indicates that the device/vehicle is in an active patrol. This is used as a passive event that indicates that an office is not in an active event, but is online and patrolling.
* **D**: (Pin Drop) drops a pin on the map; used to denote where a specific event has occurred, e.g. a suspect dropping a bag of contraband. When this signal is sent in the context of an active event, e.g. a patrol or Code 3, the pin drop is recorded against that event; when sent outside of an active event and only within the context of a patrol, the pin drop is recorded against the patrol and will only be visible in the Portal inside the details of the Patrol.
* **P**: (Pursuit) indicates the officer is in a high speed pursuit event
* **C**: (Code3) indicates that officer is in an emergency event. Usually this is triggered if the officer is travelling at high speeds to make it to an active scene.
* **[DEPRECATED]** **L**: (All clear) the device has just come out of an active event and so all active events for the device should be cleared. This was used in the Zipit system, but has been replaced by event-specific "end" signals now to support multiple active events and distinct mobile device notifications for each event
* **F**: (Power off) this signal will be sent for one of the following reasons: the device is unplugged from the PC (or power is lost) or if the Windows app is exited. When this signal is received, all active events, include any active patrol events, are ended for the given device/vehicle
* **R**: (Critical incident) this signal is sent when the officer is in a high-stress situation, e.g. an active shooter
* **M**: (Move over) the officer is on the shoulder of the road and approaching motorists should be notified of the officers presence
* **XP**: (end Pursuit) ends any active Pursuit events for the given device/vehicle
* **XC**: (end Code3) ends any active Code3 events for the given device/vehicle
* **XR**: (end Critical incident) ends any active critical incident events for the given device/vehicle
* **XM**: (end Move over) ends any active move over events for the given device/vehicle

### U
The timestamp generated on the Windows app at the time the payload is sent.

### L
The device's current latitude

### O
The device's current longitude

### S
The device's current speed in MPH. This value is calculated by the Windows app and is not generated by the GPS device.

### B
The device's current bearing in degrees. This value is calculated by the Windows app and is not generated by the GPS device.

### serial
The device's serial number
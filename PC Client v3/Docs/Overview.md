# Pursuit Alert

### Background
The project is led by Tim Morgan and Hobey Tam at Pursuit Alert. The project was born after a friend of Tim's and fellow police officer passed away after being in a high-speed pursuit. The Officer had an accident in a remote mountainous area and first responders were not able to locate his vehicle in time to provide resuce.

Pursuit Alert originally developed a device called "Blackbox" that mounts inside a police cruiser and lets the officer press a button to indicate different types of situations he or she is in. The Blackbox publishes the GPS coordinates of the vehicle via Mqtt at different intervals during each type of event. These original boxes were developed, along with all applications related to the project, excluding the Windows Client, by Zipit Wireless (namely Bill Matson and Pierre Laferriere).

The boxes cost approximately $10k to install in a typical cruiser, which limits the market for Pursuit Alert. The new, cost-effective proposal is to add another offering for organizations that provide laptops and Internet (3G/4G) access to their Officers consisting of a USB device that mimics the functionality of the existing Blackbox, but allows the connected PC to handle logic and data processing, bringing the cost to around $150 per unit.

### Companies & Responsibilities
* Raptor Wireless - USB Device & Firmware Development
* Zipit Wireless - Existing Blackbox Development, Web Portal, Mobile Client, API, Own AWS Instance
* Kopis - Windows Client Development

### Gotchas
If you add a new NuGet package, be sure to add it to the installer as well. This is made easier with the Wax for Wix extension.

### Roadmap
Zipit owns the existing infrastructure, but eventually Kopis will take over the infrastructure and migrate it into their IoT environment
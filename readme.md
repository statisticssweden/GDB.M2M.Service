# Introduction 
This is a simple console application built for sending files to GDB (VINN or KRITA) at Statistics Sweden. It's main purpose is to show how files can be sent using the M2M-api.
Please read the entire documentation at:
https://www.scb.se/vinn
or
https://www.scb.se/krita

# Getting Started
The application is pretty simple. Point out your read directory and run the application. When you add a new file to the read directory it will post the file. It uses the directory structure to define the meta data. A certificate is used for authentication. Please contact VINN- or KRITA-support regarding the certificate.

1. Install the certificate on the machine. (as the same user running the application)
2. Make sure that your `app.config` is correct. _(More information in the __Configuration__-section below)_
3. Create your folder structure. _(More information in the __Directory structure__-section below)_
4. Build the project.
5. Run the project.
6. Put the file you wish to send in _read directory_.
7. The application will send the file.

# Directory structure
The application is built to run on one server, but support differerent reportees/information providers. This is solved by using a strict directory structure to define which information provider the file belongs to. The structure is defined as such: `[read-diretory]\[organisationsnummer]\[referenceperiod]\[fileformat]`

In reality it could translate to this: `C:\Temp\999000-0045\2022-06-30\V40`

When running the application and you put a file in the directory above it will send the file as `999000-0045` and define the fileformat as `V40`. If you put the file in `C:\Temp` it will use the values in app.config instead.

# Configuration
All necessary configurations should be applied to `app.config`, mainly the `requestConfiguration`-section. The different parameters is described below:

* __ReadDirectory__ - This is the directory the application will scan for new files (including sub directories)
* __DoneDirectory__ - Sent files will be placed here after a successful post
* __CertificateSerialNumber__ - Serial number for the installed certificate. Note that the application will try to fetch the certificate as the user running the application. Currently as `new X509Store(StoreName.My, StoreLocation.CurrentUser)`. It's convenient when testing, but probably not suitable for production.
* __BaseUrl__ - Url for GDB M2M-api. Probably one of these:
 https://test.m2m.gdb.scb.se/m2m/v2/
https://m2m.gdb.scb.se/m2m/v2/
* __PingInterval__ - Used when you want to ping the api on a regular basis. Convenient when testing, but probably not suitable for production.
* __OrganisationNumber__ - Swedish Organisationnummer for the reportee. Eg. 9990000045. Not needed when using directory structure.
* __StatisticalProgram__ - Statististical program (Sv. undersökning). Either _VINN_ or _KRITA_. Not needed when using directory structure.
* __FileFormat__ - Type of form. Eg V40, V10, KRITA_MONTHLY. Not needed when using directory structure.
* __Version__ - Version of the form. Eg 1, 3, 5. Usually not needed at all, but the API supports it.
* __Referenceperiod__ - The referenceperiod (month) the file represent. Not needed when using directory structure.

# APIs Version 2
Below are the current APIs URLs to use:

## Test
* __BaseURL__ https://test.m2m.gdb.scb.se/m2m/v2/
* __Upload File Endpoint__ https://test.m2m.gdb.scb.se/m2m/v2/file/{segment}/{organisationNumber}/{statisticalProgram}/{referenceperiod}/{fileFormat}/{fileName}/{version?} 
 - Note that the largest allowed fragment size is 1024*9000 bytes
* __Current Status of a file__ https://test.m2m.gdb.scb.se/m2m/v2/history/{deliveryId} - Here DeliveryId is what is returned when uploading a file.
* __Heartbeat__ https://test.m2m.gdb.scb.se/m2m/v2/heartbeat

## Production
* __BaseURL__ https://m2m.gdb.scb.se/m2m/v2/
* __Upload File Endpoint__ https://m2m.gdb.scb.se/m2m/v2/file/{segment}/{organisationNumber}/{statisticalProgram}/{referenceperiod}/{fileFormat}/{fileName}/{version?}
 - Note that the largest allowed fragment size is 1024*9000 bytes
* __Current Status of a file__ https://m2m.gdb.scb.se/m2m/v2/history/{deliveryId} - Here DeliveryId is what is returned when uploading a file.
* __Heartbeat__ https://m2m.gdb.scb.se/m2m/v2/heartbeat

Please note that we currently only support the ContentType: __multipart/form-data__ in the http request and that {version?} is optional. 

# APIs Version 1 - Removed
If any APIs aside from version 2 is used, note that as of 2024-11-28 they no longer work and should be switched to V2.

# Notes regarding the fileupload
When uploading a file , the file is split into fragments and sent to the API. The API will then merge the fragments and save the file. The largest allowed fragment size is 1024*9000 bytes. 
Moreover, in order for the system to know when a new file is being sent or if the file is complete, the first fragment should have index 0 and the last fragment must be sent with index = -1. 
This example solution implements both the chunking and the naming of the first and last fragments, so you don't have to worry about it.
However if you want to create your own solution, you should make sure that these requirements are met.

# Build and Test
Tests are are not included in the distributed solution.

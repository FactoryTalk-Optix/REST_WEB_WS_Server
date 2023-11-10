# REST-WEB-WS_Server
Sample of hosting a REST, WEB, and Web Socket Server in Optix

All thanks, kudos, and credit to Andreas Probst

## Description

### Features

- All-in-one object to host WEB Server, REST Server, and Web Sockets in FTOptix
- Currently, no TSL is supported, but maybe in the future with the same technique that MQTT is using
- Provided as sample code
- When you use a REST Server, you would need to adjust the C# code to deal with all the different resources (Endpoints) of the server. It would be hard to have something “generic” on that part.
- If used as a Web Socket, there is a variable “Message” which is updated automatically.

## Sample Project setup

1. Copy and paste the `RATC_WebSRV` NetLogic to your project
    - Note that the build will fail after pasting. This is normal as we need to include some additional code in Optix.
    - This is the part of the code which should be adjusted based on the application’s needs.

2. Import additional code in Optix
    - Go to the sample and open Visual Studio or Visual Studio Code.
    - Copy the folder `ratc_web` to your application.
    - This contains all the common codes to handle the communication.
    - After this, the project should compile without any errors.
3. Configure the server
    - Depending on your application’s needs, you don’t need to provide all information here.

### Properties

- `ServerRunning`: An indicator if the Server is up and running
- `ClientCount`: Number of connected clients.
- `URL`: The LOCAL IP where the server should listen on (Interface bind to). After changing the IP at runtime, you need to restart the server.
- `Port`: The port used for the listener
- `CSVDataPath`: Not generic, just for a specific use-case
- `SaveToCSV`: Not generic, just for a specific use-case
- `PublicFolderWeb`: If used as a Web Server, specifies the folder with the files
- `StartAtRuntimeStart`: Set to true to start automatically
- `Message`: When used as a Web Socket, the message that is received

### Methods

Overview of functions of the Server

- `startServer()`: Starts the server. Just needed when not set to auto-start.
- `stopServer()`: Manually stop the server.
- `sendDataMsg(string msg, string channel)`: Just for Web Socket: Send a string message to all connected clients of a given channel.
- `sendDataBytes(byte[] data)`, Just for Web Socket: Send a byte[] message to all connected clients of string channel) a given channel.

### Customization

Adjust the code to support requested functions for REST

- Open the code and get the `RATC_WEBSVR`
- You will see 3 functions (events) only used for Web Socket. You can adjust/review the code if needed.
- Go to the event “app” where you will find the methods for the REST server.
- Review the event argument “e” and take a look at 2 important properties:
    - `e.req.HttpMethod`: Here is the selector of the method, like GET, POST, PUT…
    - `e.path`: Is the path what is called. If the client sends a GET command to `http://localhost/products/123abc` then the path will be `/products/123abc`
- Another property would be the `e.req.QueryString` property. This gives you a list of the URL parameters if there are any. In general, just take a look at the .r.req for the requested information.
- With those 2 you can easily change/extend/adjust all the supported commands in your application. 
- Have a look at the PUT example on how to access the body of the request.
- If you like to include any content in the response, you can do so by setting the “e.res.setContent()” function.
- There are many samples included in the code.

### Disclaimer

Rockwell Automation maintains these repositories as a convenience to you and other users. Although Rockwell Automation reserves the right at any time and for any reason to refuse access to edit or remove content from this Repository, you acknowledge and agree to accept sole responsibility and liability for any Repository content posted, transmitted, downloaded, or used by you. Rockwell Automation has no obligation to monitor or update Repository content

The examples provided are to be used as a reference for building your own application and should not be used in production as-is. It is recommended to adapt the example for the purpose, observing the highest safety standards.

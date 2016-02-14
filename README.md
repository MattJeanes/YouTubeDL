# YouTubeDL
Node.js app for downloading and streaming YouTube videos as audio

#Installation
Use a command line, change directory to the folder and run `npm install`

You will also need the following executables in the folder
- youtube-dl.exe (https://rg3.github.io/youtube-dl/)
- ffmpeg.exe (https://ffmpeg.org/download.html)
- ffprobe.exe (https://ffmpeg.org/download.html)

Note that YouTubeDL was written on and designed for Windows, but it should work on Linux with some minor changes

#Configuration
Open up server.js and there are a few variables at the top you can configure, such as max video length, the output folder names and the port that the server runs on.

#Usage
Start the app by running `node server` and wait for it to start up

You can then connect to the API using a browser, using either the 'get' or 'play' commands - with argument id (video id)

First use `/get?id=<youtubeid>` to get information about the video and request conversion. It returns a JSON table as a response containing information about the video, any errors and success state

Then use `/play?id=<youtubeid>` to request the video stream. This returns a content type of audio/mpeg and will attempt to stream the video in audio format back to the browser

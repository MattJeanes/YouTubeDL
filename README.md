# YouTubeDL
.NET Core app for streaming YouTube videos as audio

# Installation
- Pull [Docker image](https://github.com/MattJeanes/YouTubeDL/packages/328682?version=latest)
- Set ApiKey environment variable to YouTube API Key

# Usage
First use `/get?id=<youtubeid>` to get information about the video and request conversion. It returns a JSON table as a response containing information about the video, any errors and success state

Then use `/play?id=<youtubeid>` to request the video stream. This returns a content type of audio/mpeg and will attempt to stream the video in audio format back to the browser

# YouTubeDL
.NET Core app for streaming YouTube videos as audio

# Installation
- Pull [Docker image](https://github.com/users/AmyJeanes/packages/container/package/youtubedl%2Fyoutubedl-web)
- Optionally set `MaxSizeMegabytes` environment variable (default 50) to prevent large streams from being loaded
- Optionally set `TranscodeFormat` environment variable (default '') to transcode before streaming to e.g. `mp3`, `ogg`, etc
- Optionally set the `AllowUserTranscode` environment variable (default 'false') to allow user selection of transcode format with the `format` query parameter

# Usage
First use `/get?id=<youtubeid>` to get information about the video and request conversion. It returns a JSON table as a response containing information about the video, any errors and success state

Then use `/play?id=<youtubeid>` to request the video stream. This returns a content type of `audio/mpeg` by default (unless `TranscodeFormat` is set) and will attempt to stream the video in audio format back to the browser

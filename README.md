# A stream overlay ticker controlled by Discord

This project has two portions, the webpage (ticker overlay) and the Discord bot.

## Ticker

* No build required
* To be used as a browser layer in a streaming program like OBS Studio
* There are a lot of configuration options at the top of script.js

## Bot

* The bot requires a Discord token to be placed in the /ticker-bot directory before building. Get a Discord bot token and paste it in a "token" file (no quotes, no file extension)
* Build in VSCode using the scripts provided in /ticker-bot/script/win
* Build scripts are not yet cross-platform, you would have to set up a build manually in *nix 
* Some dependencies used are not built to target dotnetcore but are compatible. I could branch them and rebuild with dotnetcore as a target in the future

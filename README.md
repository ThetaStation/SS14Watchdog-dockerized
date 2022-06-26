# SS14.Watchdog 

SS14.Watchdog (codename Ian) is SS14's server-hosting wrapper thing, similar to [TGS](https://github.com/tgstation/tgstation-server) for BYOND (but much simpler for the time being). It handles auto updates, monitoring, automatic restarts, and administration. We recommend you use this for proper deployments.

Documentation on how setup and use for SS14.Watchdog is [here](https://docs.spacestation14.io/en/getting-started/hosting#ss14watchdog).

## Docker compose
Getting it up and running is simple
```docker-compose up --build```
Запустить в background
```docker-compose up --build -d```

## Configs
You probably want to change config.toml and appsettings.yml. Also remember to change the DATABASE PASSWORD

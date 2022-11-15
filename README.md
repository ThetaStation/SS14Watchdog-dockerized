# SS14.Watchdog 

SS14.Watchdog (codename Ian) is SS14's server-hosting wrapper thing, similar to [TGS](https://github.com/tgstation/tgstation-server) for BYOND (but much simpler for the time being). It handles auto updates, monitoring, automatic restarts, and administration. We recommend you use this for proper deployments.

Documentation on how setup and use for SS14.Watchdog is [here](https://docs.spacestation14.io/en/getting-started/hosting#ss14watchdog).


# Running the server

1) Copy these files (or whole repo if you want)
 - `appsettings.yml`
 - `docker-comose.yml`
 - `.env `

2) Change passwords and ports if necessary in `appsettings.yml` и `.env` (they must be the same in those two files).
3) ```docker-compose up -d```
4) the server is up and running. View the logs using the command ```docker-compose logs```

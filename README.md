"Spent some time on it yesterday and today. For my use case I'm finding trouble with a few things (latest prerelease version): 

I use a path base as well in this application. For my case this is development related but could apply to some legitimate production scenarios that utilize path based routing - ex: the same host with /appone and /apptwo should ensure all respective application paths use that root. When defining a pathbase in a blazor app this is respected, all blazor framework files and assets are remapped under that pathbase. There are a few areas where this is problematic with the dashboard:
a. The .well-known route (ex: http://localhost:5053/.well-known/masstransit-dashboard-config.js) is served from the root
b. When I hit the dashboard url (ex: http://localhost:5053/testbase/ops/masstransit) my url is rewritten to ex: http://localhost:5053/ops/masstransit/testbase/ops/masstransit resulting in a not found
c. The last problem also seems to apply to the api routes (ex: http://localhost:5053/ops/masstransit/testbase/ops/api/buses) 
I am seeing the assets resolve to the correct locations: http://localhost:5053/testbase/ops/masstransit/assets/react-core-Cx2qGhDj.js (200s)

I use a fallback authorization policy. Reason: I've never been a fan of the implication of missing a .RequireAuthorization() on an endpoint, so I require it on everything and apply my AllowAnonymous() on my assets and endpoints explicitly. I had to short circuit the /.well-known route to have AllowAnonymous so it would resolve."

--

Repro notes: 

1. Open an incognito/private window to prevent cookie persistence from interfering with the repro
2. Head to http://localhost:5053/testbase/account/register, create an account, verify the email in the followup step
3. Head to http://localhost:5053/testbase/account/login, log in 
4. Head to http://localhost:5053/testbase/ops/masstransit

Note the .well-known override in Program.cs, comment that out to test the built in behavior
Note the fallback authorization policy as well 

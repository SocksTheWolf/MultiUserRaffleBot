# MultiUser Raffle Bot

This runs a raffle over multiple accounts on Twitch, each time a team raises a factor of $100 on Tiltify.

You will need to run the application first to generate the empty `config.json` file. Fill that in with the correct information and then the application can be used properly.

Once set, restart the application and keep it running for however long your event is going. 

## Notes

* All raffle winners will contain the Twitch whisperable username of the winner in a generated raffles.txt file.
* There is no support for "restoring progress" from closing the app and opening it again. If you need to close the app, it's recommended that you remove all the completed raffles from your config file. Otherwise those previous raffles will run again.
* This connects to all of the Twitch accounts in the channels list
* If no raffle entries are made, the raffle will be closed with no winner and still documented in the output raffles.txt file.
* This only works for Tiltify team campaigns! Use your Tiltify team campaign id. The app will watch the total amount your team has raised for the campaign. 

## RaffleData Setup

This is an array of RaffleItems, of which the schema is as follows:

```
{
  "Artist": "ExampleArtist",
  "Type": "Full body piece",
  "Amount": 200.00
}
```

Gaps are allowed in the RaffleData array, and if an amount doesnt exist, the milestone will be skipped.

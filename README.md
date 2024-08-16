# MultiUser Raffle Bot

This runs a raffle over multiple accounts on Twitch, each time a team raises a factor of $100 on Tiltify.

You will need to run the application first to generate the empty `config.json` file. Fill that in with the correct information and then the application can be used properly.

## Notes

* This connects to all of the Twitch accounts in the channels list
* All raffle winners will contain the whisperable username of the winner in a generated raffles.txt file.
* If no raffle entries are made, the raffle will be closed with no winner and still documented in the output raffles.txt file.

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

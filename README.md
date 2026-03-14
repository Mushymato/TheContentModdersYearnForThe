## EveryoneIsSandy

Sandy is a fashion icon in the valley, everyone should be Sandy.

## ButAlexIsJosh

Alex is embroiled in property ownership dispute right now and had to change his name back to Josh so that he won't get evicted out of JoshHouse.

## AndWeTalkLikeAPirateOnFall19

On Fall 19, Ferngill Talk Like a Pirate Day, people will Talk Like a Pirate

## This TheContentModdersYearnForThe.dll, what does it do?

You can do content edits with content edits and they do foreach things as well as regex things.

## `TheContentModdersYearnForThe/Foreach`

```json
{"mushymato.EveryoneIsSandy_Joshed_Dialogues": {
  // target asset, either exact or a prefix
  "Target": "Characters/Dialogue/",
  // marks this as a prefix
  "TargetIsPrefix": true,
  // (Texture2D only) copy asset from another asset
  "CopyFrom": null,
  // Filter on some value(s), takes same structure as Modify + extra "Truthy": true field which controls if the condition checks for true or false
  "Filter": null,
  "Modify": [
    {
      // List of fields to drill down
      "Fields": [
        // exact field name/key
        "Key"
        // this is wildcard, applicable if this field is a dict or a list
        // does modify on every member
        "*",
        // regex, must provide Split even if empty string
        {
          "Match": "Alex",
          // Split on, only works for strings
          "Split": "(\\$[a-zA-Z]|\\[.+\\])"
        }
      ],
      // If RandValues is set then a value is picked randomly from that list, otherwise Value is used
      "Value": "[LocalizedText Strings\\NPCNames:Alex]",
      "RandValues": null
    }
  ],
  // Internal order of these foreach data edits, lower is earlier
  "Precedence": 1
}}
```

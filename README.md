---

# BaroLingua (XML Conversation Translator)

## Overview

A TUI based tool designed to localize Barotrauma XML conversation files into Russian.

## Features

- Loads conversation lines from an XML file.
- Shows all the info about a line (tags, id).
- Saves the translated lines back to the original XML file.
- Tracks the number of translated lines and remaining English lines.

## Getting Started

### Prerequisites

- .NET SDK 10

### Running

   ```bash
   git clone https://github.com/Ser1-1y/BaroLingua.git
   cd BaroLingua
   dotnet run
   ```

### Configuration

You can configure the application by modifying `config.json` (created on the first run) or through the settings menu.

### Usage

1. Run the application.
2. Input the path to the XML file you wish to translate.
3. Translate lines. Press 'Escape' to stop.

### Example XML Structure

The XML file should contain conversation nodes structured as follows:

```xml

<Conversation
        line="I've been seeing an increase in rookie captains. High schoolers, applying for submarining early in life."
        speaker="0" maxintensity="0.4">
    <Conversation line="They hold tryouts sometimes. I've heard the standards are extremely low." speaker="1">
        <Conversation line="You've got that right." speaker="0"/>
        <Conversation line="I wouldn't want any of them to captain a submarine I'm on..." speaker="2"
                      speakertags="fearful"/>
    </Conversation>
    <Conversation line="Enthusiasm hastens their demise." speaker="1" speakertags="nihilist">
        <Conversation line="I doubt that's true!" speaker="2" speakertags="optimist"/>
        <Conversation line="I can't say anything jokey. This just makes me sad." speaker="2" speakertags="joker"/>
    </Conversation>
</Conversation>
```

## Contributing

Please feel free to submit a pull request or open an issue for any enhancements or bug fixes.

## License

This project is licensed under the [Apache 2.0 License](LICENSE.txt).

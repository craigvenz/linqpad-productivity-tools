<Query Kind="Program">
  <Namespace>LINQPad.Controls</Namespace>
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

#load "SlideKit.linq"

//  ═══ SLIDE 11 · CLOSE ═══ budget 1:00 ═══
//
//  SAY: "One habit change. Next time you're about to spin up a console app
//        to check one thing — don't. Open LINQPad. That's the whole ask."
//
//  THEN: "Questions — and if you want this deck, it's twelve .linq files,
//         I'll drop them in the channel."

void Main()
{
    Slide("The Ask", "one habit change");

    Util.Markdown("""
        ### Next time you catch yourself saying this:
        > *"I'll just spin up a quick console app to check…"*

        **Don't.** Open LINQPad instead. That's the entire ask.

        ---

        ### Cheat sheet

        | Want to… | Reach for |
        |---|---|
        | see an object | `.Dump()` |
        | compare two objects | `Util.Dif` |
        | stop re-fetching | `Util.Cache` |
        | query a DB in 10 seconds | add a connection, then LINQ |
        | learn what LINQ compiles to | the **SQL panel**, `Alt+Q` |
        | read someone else's source | `.Decompile()` |
        | touch a private member | `.Uncapsulate()` |
        | work inside a real project | `#:project ..\src\App.csproj` |
        | build a tool for ops | `LINQPad.Controls` + `LPRun9-x64.exe` |
        | replace a shell script | `Util.Cmd` |
        | chart it | `.Chart().Dump()` |
        | benchmark a selection | `Ctrl+Shift+B` |
        """).Dump();
        
    Meme("Thanks for coming", "Now go delete a console app", "", "", File.ReadAllBytes(@"C:\Users\Craig.Venz\Downloads\citizen-kane-orson-welles-clapping-8xlx6s9h6qj7z9qs.gif"));

    Util.Markdown(
"""
## Links!

- LINQPad - [https://www.linqpad.net/](https://www.linqpad.net/)
- What's new in LINQPad 9 - [https://www.linqpad.net/LINQPad9.aspx](https://www.linqpad.net/LINQPad9.aspx)
- LINQPad Resources - [https://www.linqpad.net/Resources.aspx](https://www.linqpad.net/Resources.aspx)
- My LINQPad Examples Repo - [https://github.com/craigvenz/linqpad-productivity-tools](https://github.com/craigvenz/linqpad-productivity-tools)
- These Slides - [https://github.com/craigvenz/linqpad-productivity-tools/LINQPad Talk](https://github.com/craigvenz/linqpad-productivity-tools/LINQPad%20Talk)
"""
).Dump();

    Nav(11, "— questions —", TimeSpan.FromMinutes(1));
}

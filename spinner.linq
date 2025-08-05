<Query Kind="Program">
  <AutoDumpHeading>true</AutoDumpHeading>
</Query>

void Main()
{
    
}

// modified sample from linqpad samples. I didnt like the spinner circle so I did the three moving dots everyone uses nowadays.
public class Spinner : LINQPad.Controls.Control
{
    public Spinner() : base("div") => HtmlElement.InnerHtml = 
"""
<div class="bouncing-dots">
  <div></div>
  <div></div>
  <div></div>
</div>
""";

    protected override void OnRendering(EventArgs e)
    {
        Util.HtmlHead.AddStyles(
"""
.bouncing-dots {
  display: flex;
  justify-content: space-around;
  align-items: center;
  width: 50px;
}

.bouncing-dots div {
  width: 10px;
  height: 10px;
  background-color: #3498db;
  border-radius: 50%;
  animation: bounce 0.7s infinite;
}

.bouncing-dots div:nth-child(2) {
  animation-delay: 0.2s;
}

.bouncing-dots div:nth-child(3) {
  animation-delay: 0.3s;
}

@keyframes bounce {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(.75);
  }
}
""");
        base.OnRendering(e);
    }
}

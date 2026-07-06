using FOM.Server.Capture;

namespace FOM.Server.Tests;

public class CaptureAnalyzerTests
{
    [Fact]
    public void Summarizes_Traffic_Unmapped_And_Lifecycle()
    {
        string[] lines =
        [
            """{"event":"listen","detail":"127.0.0.1:7500-7545"}""",
            """{"event":"connect","conn":1,"port":7500}""",
            """{"event":"packet","dir":"C->S","opcode":"0x07D1","name":"LOGIN_REQUEST","len":24,"handled":true}""",
            """{"event":"packet","dir":"S->C","opcode":"0x07D2","name":"LOGIN_RETURN","len":620,"handled":true}""",
            """{"event":"packet","dir":"C->S","opcode":"0x0822","name":"POLL?","len":2,"handled":false}""",
            """{"event":"packet","dir":"C->S","opcode":"0x0822","name":"POLL?","len":2,"handled":false}""",
            """{"event":"packet","dir":"C->S","opcode":"0x083B","name":"KEEPALIVE30?","len":2,"handled":false}""",
            """{"event":"error","detail":"boom"}""",
            """{"event":"disconnect","conn":1,"port":7500}""",
        ];

        CaptureReport report = CaptureAnalyzer.Analyze(lines);

        Assert.Equal(5, report.TotalPackets);
        Assert.Equal(1, report.Connects);
        Assert.Equal(1, report.Disconnects);
        Assert.Equal(["boom"], report.Errors);

        // Unmapped, most-frequent first: POLL x2 then KEEPALIVE30 x1.
        Assert.Equal(2, report.Unmapped.Count);
        Assert.Equal("0x0822 POLL?", report.Unmapped[0].Key);
        Assert.Equal(2, report.Unmapped[0].Count);
        Assert.Equal("0x083B KEEPALIVE30?", report.Unmapped[1].Key);
    }

    [Fact]
    public void IgnoresBlankLines()
    {
        CaptureReport report = CaptureAnalyzer.Analyze(["", "   "]);
        Assert.Equal(0, report.TotalPackets);
        Assert.Empty(report.Unmapped);
    }
}

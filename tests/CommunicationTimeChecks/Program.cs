using Clubs;
var checks=0;
void Equal(string name,DateTimeOffset actual,string expected){if(actual!=DateTimeOffset.Parse(expected))throw new Exception(name+": "+actual);Console.WriteLine("PASS "+name);checks++;}
var overnight=new CommunicationPreference{QuietEnabled=true,QuietStart=22,QuietEnd=8};
Equal("spring quiet hours follow DST",CommunicationService.QuietUntil(overnight,"America/New_York",DateTimeOffset.Parse("2027-03-14T04:00:00Z")),"2027-03-14T12:00:00Z");
Equal("nonexistent quiet end advances to valid local time",CommunicationService.QuietUntil(new(){QuietEnabled=true,QuietStart=22,QuietEnd=2},"America/New_York",DateTimeOffset.Parse("2027-03-14T06:30:00Z")),"2027-03-14T07:00:00Z");
Equal("ambiguous quiet end uses later occurrence",CommunicationService.QuietUntil(new(){QuietEnabled=true,QuietStart=22,QuietEnd=1},"America/New_York",DateTimeOffset.Parse("2027-11-07T04:30:00Z")),"2027-11-07T06:00:00Z");
Equal("recurrence preserves local hour over spring DST",CommunicationService.NextCycle(DateTimeOffset.Parse("2027-03-13T15:00:00Z"),"America/New_York",1),"2027-03-14T14:00:00Z");
Equal("recurrence preserves local hour over autumn DST",CommunicationService.NextCycle(DateTimeOffset.Parse("2027-11-06T14:00:00Z"),"America/New_York",1),"2027-11-07T15:00:00Z");
Equal("half-hour zone quiet hours",CommunicationService.QuietUntil(overnight,"Asia/Kolkata",DateTimeOffset.Parse("2027-03-14T18:00:00Z")),"2027-03-15T02:30:00Z");
Equal("disabled quiet hours leave schedule unchanged",CommunicationService.QuietUntil(new(){QuietEnabled=false},"UTC",DateTimeOffset.Parse("2027-03-14T01:00:00Z")),"2027-03-14T01:00:00Z");
Console.WriteLine(checks+" time-zone checks passed");

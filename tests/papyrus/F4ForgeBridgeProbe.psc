Scriptname F4ForgeBridgeProbe extends Quest

Int Property Counter Auto

Struct Sample
    String Label
    Int Count
EndStruct

Int Function Add(Int left, Int right) Global
    Return left + right
EndFunction

String Function Echo(String text) Global
    Return text
EndFunction

Var[] Function EchoArray(Var[] values) Global
    Return values
EndFunction

Sample Function EchoStruct(Sample value) Global
    Return value
EndFunction

Int Function WaitAndReturn(Float seconds, Int value) Global
    Utility.Wait(seconds)
    Return value
EndFunction

Int Function Advance(Int amount)
    Counter = Counter + amount
    Return Counter
EndFunction

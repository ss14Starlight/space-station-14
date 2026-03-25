namespace Content.Shared._Starlight.Station;

[RegisterComponent]
public sealed partial class StationCrewStatisticsComponent : Component
{
    [ViewVariables] 
    public int Crew = 0;
    
    [ViewVariables] 
    public int Borgs = 0;
    
    [ViewVariables] 
    public int LostCrew = 0;
    
    [ViewVariables] 
    public int LostBorgs = 0;
    
    [ViewVariables] 
    public int EvacuatedCrew = 0;
    
    [ViewVariables] 
    public int StolenBorgs = 0;

    public void Clear()
    {
        Crew = 0;
        Borgs = 0;
        LostCrew = 0;
        LostBorgs = 0;
        EvacuatedCrew = 0;
        StolenBorgs = 0;
    }
}

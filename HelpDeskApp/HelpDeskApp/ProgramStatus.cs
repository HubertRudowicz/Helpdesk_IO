public class ProgramStatus
{
    public string Name { get; set; }
    private bool? status;
    public bool? Status
    {
        get
        {
            if (SubPrograms != null && SubPrograms.Count > 0)
            {
                // Jeśli jest chociaż jeden podprogram, to status jest true tylko gdy wszystkie są true
                foreach (var sub in SubPrograms)
                {
                    if (sub.Status != true)
                        return false;
                }
                return true;
            }
            return status;
        }
        set
        {
            status = value;
        }
    }
    public List<ProgramStatus> SubPrograms { get; set; } = new List<ProgramStatus>();
}

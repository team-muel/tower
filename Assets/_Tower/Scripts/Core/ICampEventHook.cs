using System.Collections.Generic;

namespace Tower.Core
{
    public interface ICampEventHook
    {
        void OnCampEntered(IReadOnlyList<ExpeditionMember> party);
    }
}

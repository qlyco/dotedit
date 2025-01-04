using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotEdit
{
    interface IAction
    {
        public abstract IAction ExecuteAction();
    }
}

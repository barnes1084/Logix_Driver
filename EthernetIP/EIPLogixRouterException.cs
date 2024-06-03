using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class EIPLogixRouterException : Exception
    {
        CIPMessageRouterResponse responseValue;

        public CIPMessageRouterResponse Response
        {
            get { return responseValue; }
        }


        public EIPLogixRouterException(CIPMessageRouterResponse response)
            : base($"The message router responded with with error code {response.GeneralStatus.ToString("X2")}")
        {
            responseValue = response;
        }
    }
}

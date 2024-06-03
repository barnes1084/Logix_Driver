using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    [Serializable]
    public class EIPLogixServiceException : Exception
    {
        CIPConnectedServiceResponse responseValue;

        public CIPConnectedServiceResponse Response
        {
            get { return responseValue; }
        }

        public EIPLogixServiceException(CIPConnectedServiceResponse response)
            : base($"The requested service failed with code {response.GeneralStatus.ToString("X2")}")
        {
            responseValue = response;
        }
    }
}

//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchLLM;

public class Response
{
    public bool Succeeded = false;
    public string Result = "";

    public Response() { }

    public Response(bool succeeded, string result)
    {
        Succeeded = succeeded;
        Result = result;
    }
}

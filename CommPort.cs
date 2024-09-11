using System;
using System.IO.Ports;

namespace _JMLED
{
    public class CommPort : SerialPort
    {
        private SerialPort comm = new SerialPort();
        private bool listening = false;
        private bool closing = false;
        private String message;


        public String Message
        {
            get { return this.message; }
            set { message = value; }
        }

        public bool Listenling
        {
            get { return this.listening; }
            set { listening = value; }
        }

        public bool Closing
        {
            get { return this.closing; }
            set { closing = value; }
        }


    }
}

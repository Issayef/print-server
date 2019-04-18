using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WebSocketSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PrintServer
{
    [Serializable]
    public class CallbackEventArgs : EventArgs
    {
        private string _topic;
        private JObject _data;

        internal CallbackEventArgs(string topic, JObject data)
        {
            _topic = topic;
            _data = data;
        }

        /// <summary>
        /// Получить идентификатор топика
        /// </summary>
        public string Topic
        {
            get
            {
                return _topic;
            }
        }

        /// <summary>
        /// Получить полученные данные
        /// </summary>
        public JObject Data
        {
            get
            {
                return _data;
            }
        }
    }

    enum MESSAGE_TYPEID
    {
        WELCOME,        // = 0
        PREFIX,         // = 1
        CALL,           // = 2
        CALL_RESULT,    // = 3
        CALL_ERROR,     // = 4
        SUBSCRIBE,      // = 5
        UNSUBSCRIBE,    // = 6
        PUBLISH,        // = 7
        EVENT           // = 8
    }

    public class WSClient : WebSocketSharp.WebSocket
    {
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="url"></param>
        /// <param name="protocols"></param>
        public WSClient(string url, params string[] protocols)
            : base(url, protocols)
        {
            this.OnMessage += WSClient_OnMessage;
        }

        /// <summary>
        /// Подписаться на конал
        /// </summary>
        /// <param name="topicuri"></param>
        public void Subscribe(string topicuri)
        {
            string msg = "[" + (int)MESSAGE_TYPEID.SUBSCRIBE + ", \"" + topicuri + "\"]";
            this.Send(msg);
        }

        /// <summary>
        /// Отписаться от канала
        /// </summary>
        /// <param name="topicuri"></param>
        public void UnSubscribe(string topicuri)
        {
            string msg = "[" + (int)MESSAGE_TYPEID.UNSUBSCRIBE + ", \"" + topicuri + "\"]";
            this.Send(msg);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topicuri"></param>
        /// <param name="event_data"></param>
        /// <param name="excludeMe"></param>
        /// <param name="exclude"></param>
        /// <param name="eligible"></param>
        public void Publish(string topicuri, JObject event_data, bool excludeMe = false, bool exclude = false, bool eligible = false)
        {
            string ress = event_data.ToString();
            var TextBytes = Encoding.UTF8.GetBytes(ress);
            string res = Convert.ToBase64String(TextBytes);
            Console.WriteLine(res);

            string msg = "[" + (int)MESSAGE_TYPEID.PUBLISH + ", \"" + topicuri + "\", \"" + res + "\"]";

            this.Send(msg);
        }

        /// <summary>
        /// Occurs when the WebSocket connection has been closed.
        /// </summary>
        public event EventHandler<CallbackEventArgs> OnEvent;

        /// <summary>
        /// Обработка сообщений
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void WSClient_OnMessage(object sender, MessageEventArgs e)
        {
            JToken o = JToken.Parse(e.Data);

            MESSAGE_TYPEID tp = (MESSAGE_TYPEID)Convert.ToInt32((string)o[0]);

            switch (tp)
            {
                case MESSAGE_TYPEID.CALL_RESULT:
                    break;

                case MESSAGE_TYPEID.CALL_ERROR:
                    break;

                case MESSAGE_TYPEID.WELCOME:
                    break;

                case MESSAGE_TYPEID.EVENT:


                    if (OnEvent != null)
                    {
                        string topic = (string)o[1];
                        JObject data = (JObject)o[2];

                        OnEvent(this, new CallbackEventArgs(
                                topic,
                                data
                            ));
                    }
                    break;

            }
        }

    }
}

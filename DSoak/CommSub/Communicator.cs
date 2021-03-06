﻿using System;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using Messages;
using SharedObjects;
using log4net;

namespace CommSub
{
    public class Communicator
    {
        #region Private Data Members
        private readonly ILog _logger;
        private readonly ILog _loggerDeep;

        private UdpClient _myUdpClient;
        private static readonly object StartLock = new object();
        #endregion

        #region Public Properties
        public int MinPort { get; set; }
        public int MaxPort { get; set; }
        public int Port { get { return (_myUdpClient != null) ? ((IPEndPoint)_myUdpClient.Client.LocalEndPoint).Port : 0; } }

        #endregion

        public Communicator(string loggerPrefix = null)
        {
            loggerPrefix = string.IsNullOrWhiteSpace(loggerPrefix) ? "" : loggerPrefix.Trim() + "_";

             _logger = LogManager.GetLogger(loggerPrefix + typeof(Communicator));
             _loggerDeep = LogManager.GetLogger(loggerPrefix + typeof(Communicator) + "_Deep");   
        }


        #region Public Methods
        public void Start()
        {
            _logger.Info("Start communicator");
            bool bindSuccessfull = false;

            ValidPorts();

            lock (StartLock)
            {
                int portToTry = FindAvailablePort(MinPort, MaxPort);
                if (portToTry > 0)
                {
                    try
                    {
                        IPEndPoint localEp = new IPEndPoint(IPAddress.Any, portToTry);
                        _myUdpClient = new UdpClient(localEp);
                        bindSuccessfull = true;
                    }
                    catch (SocketException)
                    {
                    }
                }
                if (!bindSuccessfull)
                    throw new ApplicationException(string.Format("Cannot bind the socket to a port {0}", portToTry));
            }
        }

        public void Stop()
        {
            _logger.Info("Stop of communicator");
            if (_myUdpClient != null)
            {
                _myUdpClient.Close();
                _myUdpClient = null;
            }
        }

        public int IncomingAvailable()
        {
            return ((_myUdpClient==null) ? 0 : _myUdpClient.Available);
        }

        public Envelope Receive(int timeout = 0)
        {
            Envelope result = null;

            IPEndPoint ep;
            byte[] receivedBytes = ReceiveBytes(timeout, out ep);
            if (receivedBytes != null && receivedBytes.Length>0)
            {
                PublicEndPoint pep = new PublicEndPoint() { IPEndPoint = ep };
                Message message = Message.Decode(receivedBytes);
                if (message != null)
                {
                    result = new Envelope(message, pep);
                    _logger.DebugFormat("Just received message, Nr={0}, Conv={1}, Type={2}, From={3}",
                        (result.Message.MsgId==null) ? "null" : result.Message.MsgId.ToString(),
                        (result.Message.ConvId==null) ? "null" : result.Message.ConvId.ToString(),
                        result.Message.GetType().Name,
                        (result.IPEndPoint==null) ? "null" : result.IPEndPoint.ToString());
                }
                else
                {
                    _logger.ErrorFormat("Cannot decode message received from {0}", pep);
                    string tmp = Encoding.ASCII.GetString(receivedBytes);
                    _logger.ErrorFormat("Message={0}", tmp);
                }
            }
        
            return result;
        }

        public bool Send(Envelope outgoingEnvelope)
        {
            bool result = false;
            if (outgoingEnvelope == null || !outgoingEnvelope.IsValidToSend)
                _logger.Warn("Invalid Envelope or Message");
            else
            {
                byte[] bytesToSend =outgoingEnvelope.Message.Encode();

                _logger.DebugFormat("Send out: {0} to {1}", Encoding.ASCII.GetString(bytesToSend), outgoingEnvelope.EndPoint);

                try
                {
                    _myUdpClient.Send(bytesToSend, bytesToSend.Length, outgoingEnvelope.EndPoint.IPEndPoint);
                    result = true;
                    _loggerDeep.Debug("Send complete");
                }
                catch (Exception err)
                {
                    _logger.Warn(err.ToString());
                }
            }
            return result;
        }

        #endregion

        #region Private Methods

        private byte[] ReceiveBytes(int timeout, out IPEndPoint ep)
        {
            byte[] receivedBytes = null;
            ep = null;
            if (_myUdpClient != null)
            {
                _myUdpClient.Client.ReceiveTimeout = timeout;
                ep = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    _loggerDeep.Debug("Try receive bytes from anywhere");
                    receivedBytes = _myUdpClient.Receive(ref ep);
                    _loggerDeep.Debug("Back from receive");

                    if (_logger.IsDebugEnabled)
                    {
                        if (receivedBytes != null)
                        {
                            string tmp = Encoding.ASCII.GetString(receivedBytes);
                            _logger.DebugFormat("Incoming message={0}", tmp);
                        }
                    }
                }
                catch (SocketException err)
                {
                    if (err.SocketErrorCode != SocketError.TimedOut && err.SocketErrorCode != SocketError.Interrupted)
                        _logger.Warn(err.Message);
                }
                catch (Exception err)
                {
                    _logger.Warn(err.Message);
                }
            }
            return receivedBytes;
        }

        private void ValidPorts()
        {
            if ((MinPort != 0 && (MinPort < IPEndPoint.MinPort || MinPort > IPEndPoint.MaxPort)) ||
                (MaxPort != 0 && (MaxPort < IPEndPoint.MinPort || MaxPort > IPEndPoint.MaxPort)))
                throw new ApplicationException("Invalid port specifications");
        }

        private int FindAvailablePort(int minPort, int maxPort)
        {
            int availablePort = -1;

            _logger.DebugFormat("Find a free port between {0} and {1}", minPort, maxPort);
            for (int possiblePort = minPort; possiblePort <= maxPort; possiblePort++)
            {
                if (!IsUsed(possiblePort))
                {
                    availablePort = possiblePort;
                    break;
                }
            }
            _logger.DebugFormat("Available Port = {0}", availablePort);
            return availablePort;
        }

        private bool IsUsed(int port)
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] endPoints = properties.GetActiveUdpListeners();
            return endPoints.Any(ep => ep.Port == port);
        }
        #endregion

    }
}

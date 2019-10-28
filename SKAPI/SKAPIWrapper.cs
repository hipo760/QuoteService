using System;
using System.Runtime.InteropServices;
using SKCOMLib;

namespace SKAPI
{
    public class SkapiWrapper:IDisposable
    {
        private SKCenterLib _skCenter;
        private SKReplyLib _skReply;
        private SKQuoteLib _skQuotes;

        
        public void InitSkcomLib()
        {
            _skCenter = new SKCenterLib();
            _skReply = new SKReplyLib();
            _skQuotes = new SKQuoteLib();
        }
        public void ReleaseSkcomLib()
        {
            if (_skReply != null) Marshal.ReleaseComObject(_skReply);
            if (_skQuotes != null) Marshal.ReleaseComObject(_skQuotes);
            if (_skCenter != null) Marshal.ReleaseComObject(_skCenter);
        }

        // Event
        public virtual event _ISKQuoteLibEvents_OnConnectionEventHandler OnConnectionEvent
        {
            add => _skQuotes.OnConnection += value;
            remove => _skQuotes.OnConnection -= value;
        }
        public virtual event _ISKQuoteLibEvents_OnNotifyTicksEventHandler OnNotifyTicksEvent
        {
            add => _skQuotes.OnNotifyTicks += value;
            remove => _skQuotes.OnNotifyTicks -= value;
        }
        public virtual event _ISKQuoteLibEvents_OnNotifyHistoryTicksEventHandler OnNotifyHistoryTicksEvent
        {
            add => _skQuotes.OnNotifyHistoryTicks += value;
            remove => _skQuotes.OnNotifyHistoryTicks -= value;
        }
        public virtual event _ISKQuoteLibEvents_OnNotifyServerTimeEventHandler OnNotifyServerTimeEvent
        {
            add => _skQuotes.OnNotifyServerTime += value;
            remove => _skQuotes.OnNotifyServerTime -= value;
        }
        
        // Method
        // SKCenter
        public virtual int SKCenterLib_Login(string id, string pwd) => _skCenter.SKCenterLib_Login(id, pwd);
        public virtual string SKCenterLib_GetReturnCodeMessage(int code) => _skCenter.SKCenterLib_GetReturnCodeMessage(code);


        // SKQuote
        public virtual int SKQuoteLib_EnterMonitor() => _skQuotes.SKQuoteLib_EnterMonitor();
        public virtual int SKQuoteLib_LeaveMonitor() => _skQuotes.SKQuoteLib_LeaveMonitor();
        public virtual int SKQuoteLib_RequestTicks(ref short tickPage, string symbol) => _skQuotes.SKQuoteLib_RequestTicks(ref tickPage, symbol);

        public virtual short GetSkStockIdxBySymbol(string symbol)
        {
            SKSTOCK refStock = new SKSTOCK();
            var code = _skQuotes.SKQuoteLib_GetStockByNo(symbol, ref refStock);
            return (code == 0) ? refStock.sStockIdx : (short)-1;
        }

        public void Dispose()
        {
            ReleaseSkcomLib();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Data.SQLite;
using System.Data;
using BuyLottery.Common;

namespace BuyLottery.DataAccess
{
    class DAHelper
    {
        public static void AddTickets(
            string id,
            string creator,
            string title,
            decimal amount,
            decimal price,
            decimal progress,
            string url,
            string status)
        {
            if (GetTicket(id) == null)
            {
                //Add
                string cmdText = @"insert into Tickets(id,creator,title,amount,price,progress,url,last_modify_time,status) values(?,?,?,?,?,?,?,?,?)";
                SqliteHelper.ExecuteNonQuery(cmdText,
                    id,
                    creator,
                    title,
                    amount,
                    price,
                    progress,
                    url,
                    DateTime.Now,
                    status);
            }
            else
            { 
                //Update the progress and last_modify_time
                string cmdText = @"update Tickets set progress=?, last_modify_time=? where id=?";
                SqliteHelper.ExecuteNonQuery(cmdText,
                    progress,
                    DateTime.Now,
                    id);

            }

        }

        public static DataTable GetTickets()
        {
            string cmdText = @"select id, creator,title,amount,price,progress,url,last_modify_time,status from Tickets where status<>?";
            return SqliteHelper.ExecuteDataset(
                cmdText,
                TicketStatus.HasBeenBought).Tables[0];
        }

        public static DataRow GetTicket(string id)
        {
            string cmdText = @"select id, creator,title,amount,price,progress,url,last_modify_time 
                               from Tickets 
                               where id=?";
            return SqliteHelper.ExecuteDataRow(cmdText, id);
        }

        public static void ClearTickets()
        {
            string cmdText = "delete from Tickets where status<>?";
            SqliteHelper.ExecuteNonQuery(cmdText,TicketStatus.HasBeenBought);
        }

        internal static void UpdateTicketStatus(string ticketId, string ticketStatus)
        {
            string cmdText = @"update Tickets set status=?, last_modify_time=? where id=?";
            SqliteHelper.ExecuteNonQuery(cmdText,
                ticketStatus,
                DateTime.Now,
                ticketId);
        }
    }
}

using Project_Vote.Models;
using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

public class DatabaseManager
{
    private string _connectionString;

    public DatabaseManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<Poll> SearchPollsByTitle(string title)
    {
        List<Poll> polls = new List<Poll>();

        using (MySqlConnection connection = new MySqlConnection(_connectionString))
        {
            connection.Open();

            string query = "SELECT * FROM Polls WHERE Title LIKE @Title";
            using (MySqlCommand command = new MySqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@Title", $"%{title}%");

                using (MySqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Poll poll = new Poll
                        {
                            Id = reader.GetInt32("Id"),
                            Title = reader.GetString("Title"),
                            // ... other properties ...
                        };
                        polls.Add(poll);
                    }
                }
            }
        }

        return polls;
    }
} 
﻿using eShop.OrderAPI.Messages;
using eShop.OrderAPI.Repository;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace eShop.OrderAPI.MessageConsumer
{
    public class RabbitMQPaymentConsumer : BackgroundService
    {
        private readonly OrderRepository _repository;
        private IConnection _connection;
        private IModel _channel;

        public RabbitMQPaymentConsumer(OrderRepository repository, IConnection connection, IModel channel)
        {
            _repository = repository;
            _connection = connection;
            _channel = channel;
        }

        public RabbitMQPaymentConsumer(OrderRepository repository)
        {
            _repository = repository;

            var factory = new ConnectionFactory
            {
                HostName = "127.0.0.1",
                UserName = "dnr",
                Password = "dnr123"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "orderpaymentresultqueue", false, false, false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += (channel, evt) =>
            {
                var content = Encoding.UTF8.GetString(evt.Body.ToArray());
                UpdatePaymentResultVO vo = JsonSerializer.Deserialize<UpdatePaymentResultVO>(content);
                UpdatePaymentStatus(vo).GetAwaiter().GetResult();
                _channel.BasicAck(evt.DeliveryTag, false);
            };
            _channel.BasicConsume("orderpaymentresultqueue", false, consumer);

            return Task.CompletedTask;
        }

        private async Task UpdatePaymentStatus(UpdatePaymentResultVO vo)
        {
            try
            {
                await _repository.UpdateOrderPaymentStatus(vo.OrderId, vo.Status);
            }
            catch (Exception ex)
            {
                throw new Exception("Error sender payment RabbitMQ queue", ex.InnerException);
            }
        }
    }
}
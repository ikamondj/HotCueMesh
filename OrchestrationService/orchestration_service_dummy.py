#!/usr/bin/env python3
"""
Basic Orchestration Service Dummy
Listens on TCP port 8112 for HotcueEvent JSON messages from virtual-dj-plugin
and logs all incoming data.
"""

import socket
import json
import logging
import threading
from datetime import datetime

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('orchestration_service_dummy.log'),
        logging.StreamHandler()
    ]
)

logger = logging.getLogger(__name__)

# Configuration
HOST = '0.0.0.0'  # Listen on all available interfaces
PORT = 8112  # Same port as Go orchestration service
BUFFER_SIZE = 16 * 1024


def handle_client(conn, addr):
    """Handle incoming client connection and receive JSON messages."""
    logger.info(f"Client connected from {addr}")

    try:
        while True:
            # Receive data from client
            data = conn.recv(BUFFER_SIZE)

            if not data:
                logger.info(f"Client {addr} disconnected")
                break

            # Try to parse as JSON
            try:
                message = data.decode('utf-8')
                # Handle potential multiple JSON objects separated by newlines
                for line in message.strip().split('\n'):
                    if line:
                        event = json.loads(line)
                        logger.info(f"Received from {addr}: {json.dumps(event)}")
            except json.JSONDecodeError as e:
                logger.warning(f"Failed to parse JSON from {addr}: {e}")
                logger.debug(f"Raw data: {data}")
            except UnicodeDecodeError as e:
                logger.warning(f"Failed to decode UTF-8 from {addr}: {e}")

    except Exception as e:
        logger.error(f"Error handling client {addr}: {e}")
    finally:
        conn.close()
        logger.info(f"Connection with {addr} closed")


def start_server():
    """Start the TCP orchestration service dummy."""
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

    try:
        server_socket.bind((HOST, PORT))
        server_socket.listen(5)
        logger.info(f"Orchestration Service Dummy started on {HOST}:{PORT}")
        logger.info("Listening for incoming HotcueEvent messages...")

        while True:
            # Accept incoming connections
            conn, addr = server_socket.accept()

            # Handle each client in a separate thread
            client_thread = threading.Thread(
                target=handle_client,
                args=(conn, addr),
                daemon=True
            )
            client_thread.start()

    except KeyboardInterrupt:
        logger.info("Shutting down...")
    except Exception as e:
        logger.error(f"Server error: {e}")
    finally:
        server_socket.close()
        logger.info("Server stopped")


if __name__ == '__main__':
    start_server()

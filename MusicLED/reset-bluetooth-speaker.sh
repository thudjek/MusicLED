#!/bin/bash

#############################################
# Reset Raspberry Pi Bluetooth to Default
#############################################

echo "================================================"
echo "Resetting Bluetooth Settings to Default"
echo "================================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to check if command succeeded
check_status() {
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ $1${NC}"
    else
        echo -e "${YELLOW}⚠ $1 (may already be reset)${NC}"
    fi
}

# 1. Kill bt-agent
echo -e "${YELLOW}Stopping auto-accept agent...${NC}"
if [ -f /tmp/bt-agent.pid ]; then
    PID=$(cat /tmp/bt-agent.pid)
    kill $PID 2>/dev/null
    rm /tmp/bt-agent.pid
    check_status "Stopped bt-agent (PID: $PID)"
else
    pkill bt-agent 2>/dev/null
    check_status "Stopped any bt-agent processes"
fi
echo ""

# 2. Remove ALL paired devices (to prevent auto-reconnect issues)
echo -e "${YELLOW}Removing all paired devices...${NC}"
DEVICES=$(bluetoothctl devices Paired | cut -d' ' -f2)
if [ -z "$DEVICES" ]; then
    echo "No paired devices found"
else
    DEVICE_COUNT=0
    for device in $DEVICES; do
        DEVICE_NAME=$(bluetoothctl info $device | grep "Name:" | cut -d':' -f2 | xargs)
        echo "Unpairing device: $DEVICE_NAME ($device)"
        bluetoothctl remove $device
        DEVICE_COUNT=$((DEVICE_COUNT + 1))
    done
    echo "✓ Removed $DEVICE_COUNT paired device(s)"
fi
echo ""

# 3. Reset Bluetooth adapter settings
echo -e "${YELLOW}Resetting Bluetooth adapter...${NC}"

# Turn off discoverability
sudo hciconfig hci0 noscan
check_status "Disabled discoverability"

# Restore original settings if saved
if [ -f /tmp/bluetooth-original.txt ]; then
    echo "Restoring original settings..."
    ORIGINAL_NAME=$(grep "Original name:" /tmp/bluetooth-original.txt | cut -d':' -f2- | xargs)
    ORIGINAL_CLASS=$(grep "Original class:" /tmp/bluetooth-original.txt | cut -d':' -f2 | xargs)

    if [ ! -z "$ORIGINAL_NAME" ]; then
        sudo hciconfig hci0 name "$ORIGINAL_NAME"
        check_status "Restored name to '$ORIGINAL_NAME'"
    fi

    if [ ! -z "$ORIGINAL_CLASS" ]; then
        sudo hciconfig hci0 class $ORIGINAL_CLASS
        check_status "Restored class to $ORIGINAL_CLASS"
    fi
else
    # Set to default values
    sudo hciconfig hci0 name "$(hostname)"
    check_status "Set name to hostname: $(hostname)"

    sudo hciconfig hci0 class 0x0c0104
    check_status "Set class to computer/laptop"
fi
echo ""

# 4. Reset via bluetoothctl
echo -e "${YELLOW}Resetting Bluetooth settings...${NC}"
echo -e "discoverable off\npairable off\ndiscoverable-timeout 300\nagent off\nquit" | bluetoothctl > /dev/null 2>&1
check_status "Reset bluetoothctl settings"
echo ""

# 5. Clean up saved settings
echo -e "${YELLOW}Cleaning up...${NC}"
if [ -f /tmp/bluetooth-original.txt ]; then
    rm /tmp/bluetooth-original.txt
    echo "Removed temporary settings file"
else
    echo "No temporary files to clean"
fi
echo ""

# 7. Full reset - restart Bluetooth service
echo -e "${YELLOW}Restarting Bluetooth service...${NC}"
sudo systemctl restart bluetooth
check_status "Bluetooth service restarted"
echo ""

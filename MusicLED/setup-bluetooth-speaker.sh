#!/bin/bash

#############################################
# Setup Raspberry Pi as Bluetooth Speaker
#############################################

echo "================================================"
echo "Setting up Raspberry Pi as Bluetooth Speaker"
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
        echo -e "${RED}✗ $1 failed${NC}"
    fi
}

# 1. Save current settings for later restoration
echo -e "${YELLOW}Saving current settings...${NC}"
ORIGINAL_NAME=$(sudo hciconfig hci0 name | grep Name | cut -d"'" -f2)
ORIGINAL_CLASS=$(sudo hciconfig hci0 class | grep Class | cut -d' ' -f2)
echo "Original name: $ORIGINAL_NAME" > /tmp/bluetooth-original.txt
echo "Original class: $ORIGINAL_CLASS" >> /tmp/bluetooth-original.txt
check_status "Settings saved to /tmp/bluetooth-original.txt"
echo ""

# 2. Check if PipeWire is running and load Bluetooth modules
echo -e "${YELLOW}Checking audio server...${NC}"
if pgrep -x pipewire > /dev/null; then
    echo "PipeWire is running ✓"
elif pgrep -x pulseaudio > /dev/null; then
    echo "PulseAudio is running ✓"
else
    echo "⚠️ No audio server detected (PipeWire/PulseAudio)"
fi
echo ""

# 3. Load Bluetooth audio modules
echo -e "${YELLOW}Loading Bluetooth audio modules...${NC}"
pactl load-module module-bluetooth-discover 2>/dev/null
pactl load-module module-bluetooth-policy 2>/dev/null
echo "Bluetooth modules loaded (these may show as failed on PipeWire, but that's normal)"
echo ""

# 4. Ensure WirePlumber Bluetooth support is running
echo -e "${YELLOW}Checking Bluetooth audio support...${NC}"
if systemctl --user is-active --quiet wireplumber; then
    echo "WirePlumber is running ✓"
    # Restart WirePlumber to ensure Bluetooth monitor loads
    systemctl --user restart wireplumber
    sleep 2
    echo "WirePlumber restarted"
else
    echo "⚠️ WirePlumber is not running"
fi
echo ""

# 5. Configure Bluetooth adapter
echo -e "${YELLOW}Configuring Bluetooth adapter...${NC}"

# Set device name
sudo hciconfig hci0 name "Pi Speaker"
check_status "Set name to 'Pi Speaker'"

# Set device class to audio/speaker
sudo hciconfig hci0 class 0x20041C
check_status "Set device class to speaker"

# Make discoverable and pairable
sudo hciconfig hci0 piscan
check_status "Made discoverable and pairable"
echo ""

# 6. Set up auto-accept pairing
echo -e "${YELLOW}Setting up auto-accept pairing...${NC}"
# Kill any existing bt-agent
pkill bt-agent 2>/dev/null

# Start bt-agent in background
bt-agent -c NoInputNoOutput > /dev/null 2>&1 &
echo $! > /tmp/bt-agent.pid
check_status "Auto-accept agent started (PID: $(cat /tmp/bt-agent.pid))"
echo ""

# 7. Configure Bluetooth via bluetoothctl
echo -e "${YELLOW}Additional Bluetooth configuration...${NC}"
bluetoothctl << EOF > /dev/null 2>&1
power on
discoverable on
pairable on
discoverable-timeout 0
exit
EOF
check_status "Bluetooth configured via bluetoothctl"
echo ""

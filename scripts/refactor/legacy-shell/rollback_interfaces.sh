#!/bin/bash

# Move interfaces back to root
mv WebHomestay/Services/Booking/I*.cs WebHomestay/Services/ 2>/dev/null
mv WebHomestay/Services/Slots/I*.cs WebHomestay/Services/ 2>/dev/null
mv WebHomestay/Services/Room/I*.cs WebHomestay/Services/ 2>/dev/null
mv WebHomestay/Services/Branch/I*.cs WebHomestay/Services/ 2>/dev/null
mv WebHomestay/Services/Chat/I*.cs WebHomestay/Services/ 2>/dev/null
mv WebHomestay/Services/Settings/I*.cs WebHomestay/Services/ 2>/dev/null
mv WebHomestay/Services/Infrastructure/I*.cs WebHomestay/Services/ 2>/dev/null

# Restore namespace
for file in WebHomestay/Services/I*.cs; do
    [ -f "$file" ] && sed -i 's/namespace WebHomestay.Services\.[A-Za-z]*/namespace WebHomestay.Services/' "$file"
done

echo "Rolled back interfaces to Services root"

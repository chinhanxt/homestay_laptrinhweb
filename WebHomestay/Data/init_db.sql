-- Database Schema for Web Homestay Self Check-in

-- 1. Bảng Người dùng
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    full_name VARCHAR(100),
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,
    phone_number VARCHAR(20),
    role VARCHAR(20) DEFAULT 'Customer', -- Admin, Customer
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. Bảng Phòng
CREATE TABLE IF NOT EXISTS rooms (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    price_per_hour DECIMAL(10, 2),
    price_per_day DECIMAL(10, 2),
    capacity INT DEFAULT 2,
    status VARCHAR(20) DEFAULT 'Available', -- Available, Occupied, Maintenance
    image_url TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 3. Bảng Tiện nghi
CREATE TABLE IF NOT EXISTS amenities (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) UNIQUE NOT NULL,
    icon_class VARCHAR(50) -- Dùng cho FontAwesome hoặc Icon
);

-- 4. Bảng liên kết Phòng - Tiện nghi
CREATE TABLE IF NOT EXISTS room_amenities (
    room_id INT REFERENCES rooms(id) ON DELETE CASCADE,
    amenity_id INT REFERENCES amenities(id) ON DELETE CASCADE,
    PRIMARY KEY (room_id, amenity_id)
);

-- 5. Bảng Đặt phòng (Booking)
CREATE TABLE IF NOT EXISTS bookings (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    room_id INT REFERENCES rooms(id),
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    total_price DECIMAL(10, 2),
    status VARCHAR(20) DEFAULT 'Pending', -- Pending, Confirmed, CheckedIn, CheckedOut, Cancelled
    payment_status VARCHAR(20) DEFAULT 'Unpaid', -- Unpaid, Paid
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Dữ liệu mẫu ban đầu
INSERT INTO amenities (name, icon_class) VALUES 
('Wifi', 'fa-wifi'),
('Điều hòa', 'fa-snowflake'),
('Máy chiếu', 'fa-video'),
('Bếp', 'fa-utensils');

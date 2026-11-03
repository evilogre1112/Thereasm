public enum CardStatus 
{ 
    Healthy,
    Infected,
    Dead
}

public enum SecretRole 
{ 
    Villager,
    Cur,        // Kẻ nguyền rủa (do Player 1 chọn)
    Neutral    // Trung lập, không thuộc riêng phe nào.
}

public enum CardType 
{ 
    Villager,
    Hunter,     // Thợ săn (dành riêng cho lá số 9)
    Neutral    // Trung lập (dành cho lá số 17 trở lên, nếu có)
}

public enum DefaultOwner
{
    Player1,   // Cầm lá 1-8
    Player2,    // Cầm lá 9-16
    Neutral    // Trung lập (17 trở lên, nếu có)
}
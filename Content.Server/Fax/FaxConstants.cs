namespace Content.Server.Fax;

public static class FaxConstants
{
    // Commands

    /**
     * Used to get other faxes connected to current network
     */
    public const string FaxPingCommand = "fax_ping";

    /**
     * Used as response to ping command
     */
    public const string FaxPongCommand = "fax_pong";

    /**
     * Used when fax sending data to destination fax
     */
    public const string FaxPrintCommand = "fax_print";

    // Data

    public const string FaxNameData = "fax_data_name";
    public const string FaxPaperNameData = "fax_data_title";
    public const string FaxPaperLabelData = "fax_data_label";
    public const string FaxPaperPrototypeData = "fax_data_prototype";
    public const string FaxPaperContentData = "fax_data_content";
    public const string FaxPaperStampStateData = "fax_data_stamp_state";
    public const string FaxPaperStampedByData = "fax_data_stamped_by";
    public const string FaxSyndicateData = "fax_data_i_am_syndicate";
    public const string FaxPaperLockedData = "fax_data_locked";
    
    // starlight-start
    // Cargo Slips Data
    
    public const string FaxSlipProduct = "fax_slip_product_id";
    public const string FaxSlipRequester = "fax_slip_requester";
    public const string FaxSlipReason = "fax_slip_reason";
    public const string FaxSlipOrderQuantity = "fax_slip_order_quantity";
    public const string FaxSlipOrderAccount = "fax_slip_order_account";
    // starlight-end
}

using AdNoir.Shared.Contracts.Messages;
using AdNoir.Shared.Contracts.Responses;
using Shouldly;
using Xunit;

namespace AdNoir.UnitTests.Responses;

/// <summary>The response envelope contract.</summary>
public sealed class BaseResponseTests
{
    private sealed record UserDto(Guid Id, string Name);

    [Fact]
    public void Success_carries_the_payload_and_reports_success()
    {
        UserDto dto = new(Guid.CreateVersion7(), "Ada");

        BaseResponse<UserDto> response = BaseResponse<UserDto>.Success(dto);

        response.IsSuccess.ShouldBeTrue();
        response.Status.ShouldBe(ResponseStatus.Success);
        response.Data.ShouldBe(dto);
        response.Errors.ShouldBeEmpty();
        response.MessageCode.ShouldBe(MessageCodes.Common.Success);
    }

    [Theory]
    [InlineData(ResponseStatus.Success)]
    [InlineData(ResponseStatus.Created)]
    [InlineData(ResponseStatus.Accepted)]
    [InlineData(ResponseStatus.NoContent)]
    public void IsSuccess_is_true_for_every_successful_status(ResponseStatus status)
    {
        BaseResponse response = BaseResponse.Failure(status, MessageCodes.Common.Success);

        response.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ResponseStatus.NotFound)]
    [InlineData(ResponseStatus.ValidationFailed)]
    [InlineData(ResponseStatus.Conflict)]
    [InlineData(ResponseStatus.Unauthorized)]
    [InlineData(ResponseStatus.Forbidden)]
    [InlineData(ResponseStatus.BusinessRuleViolation)]
    [InlineData(ResponseStatus.ConcurrencyConflict)]
    [InlineData(ResponseStatus.RateLimited)]
    [InlineData(ResponseStatus.ExternalServiceError)]
    [InlineData(ResponseStatus.UnexpectedError)]
    public void IsSuccess_is_false_for_every_failure_status(ResponseStatus status)
    {
        BaseResponse response = BaseResponse.Failure(status, MessageCodes.Common.UnexpectedError);

        // IsSuccess is derived rather than stored precisely so this can never disagree with
        // Status; a stored flag would let "IsSuccess = true, Status = NotFound" exist.
        response.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void NotFound_records_the_code_and_its_arguments()
    {
        Guid tenantId = Guid.CreateVersion7();

        BaseResponse<UserDto> response =
            BaseResponse<UserDto>.NotFound(MessageCodes.Tenant.NotFound, ("tenantId", tenantId));

        response.Status.ShouldBe(ResponseStatus.NotFound);
        response.MessageCode.ShouldBe(MessageCodes.Tenant.NotFound);
        response.MessageArgs.ShouldNotBeNull();
        response.MessageArgs!["tenantId"].ShouldBe(tenantId);
        response.Errors.ShouldHaveSingleItem().Code.ShouldBe(MessageCodes.Tenant.NotFound);
        response.Data.ShouldBeNull();
    }

    [Fact]
    public void Message_is_left_for_the_boundary_to_resolve()
    {
        // Plan C.6: service code sets the code, never the text.
        BaseResponse response = BaseResponse.NotFound();

        response.Message.ShouldBeNull();
        response.CorrelationId.ShouldBeNull();
        response.TraceId.ShouldBeNull();
    }

    [Fact]
    public void Map_transforms_the_payload_when_successful()
    {
        BaseResponse<int> response = BaseResponse<int>.Success(21);

        BaseResponse<string> mapped = response.Map(value => (value * 2).ToString(System.Globalization.CultureInfo.InvariantCulture));

        mapped.IsSuccess.ShouldBeTrue();
        mapped.Data.ShouldBe("42");
    }

    [Fact]
    public void Map_carries_a_failure_through_without_running_the_mapper()
    {
        bool mapperRan = false;
        BaseResponse<int> response = BaseResponse<int>.NotFound(MessageCodes.Common.NotFound);

        BaseResponse<string> mapped = response.Map(value =>
        {
            mapperRan = true;
            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });

        mapperRan.ShouldBeFalse();
        mapped.Status.ShouldBe(ResponseStatus.NotFound);
        mapped.MessageCode.ShouldBe(MessageCodes.Common.NotFound);
    }

    [Fact]
    public void Bind_short_circuits_on_the_first_failure()
    {
        bool secondStepRan = false;

        BaseResponse<string> result = BaseResponse<int>
            .Conflict(MessageCodes.Common.Conflict)
            .Bind(value =>
            {
                secondStepRan = true;
                return BaseResponse<string>.Success(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            });

        secondStepRan.ShouldBeFalse();
        result.Status.ShouldBe(ResponseStatus.Conflict);
    }

    [Fact]
    public void Ensure_turns_a_success_into_a_failure_when_the_check_fails()
    {
        BaseResponse<int> response = BaseResponse<int>
            .Success(5)
            .Ensure(value => value > 10, MessageCodes.Common.BusinessRuleViolation);

        response.IsSuccess.ShouldBeFalse();
        response.Status.ShouldBe(ResponseStatus.BusinessRuleViolation);
    }

    [Fact]
    public void Ensure_leaves_a_passing_response_untouched()
    {
        BaseResponse<int> response = BaseResponse<int>
            .Success(50)
            .Ensure(value => value > 10, MessageCodes.Common.BusinessRuleViolation);

        response.IsSuccess.ShouldBeTrue();
        response.Data.ShouldBe(50);
    }

    [Fact]
    public void Propagate_preserves_the_diagnostics_while_changing_the_payload_type()
    {
        BaseResponse source = BaseResponse.Failure(
            ResponseStatus.Forbidden,
            MessageCodes.Auth.PermissionDenied,
            ("permission", "user:write"));

        BaseResponse<UserDto> propagated = BaseResponse<UserDto>.Propagate(source);

        propagated.Status.ShouldBe(ResponseStatus.Forbidden);
        propagated.MessageCode.ShouldBe(MessageCodes.Auth.PermissionDenied);
        propagated.MessageArgs!["permission"].ShouldBe("user:write");
        propagated.Errors.Count.ShouldBe(source.Errors.Count);
    }

    [Fact]
    public void Propagate_refuses_a_successful_source()
    {
        BaseResponse source = BaseResponse.Success();

        Should.Throw<InvalidOperationException>(() => BaseResponse<UserDto>.Propagate(source));
    }

    [Fact]
    public void Implicit_conversion_produces_a_plain_success()
    {
        // The conversion cannot guess Created or Accepted, so it always means Success.
        UserDto dto = new(Guid.CreateVersion7(), "Ada");

        BaseResponse<UserDto> response = dto;

        response.Status.ShouldBe(ResponseStatus.Success);
        response.Data.ShouldBe(dto);
    }

    [Fact]
    public void Match_selects_the_branch_that_matches_the_outcome()
    {
        BaseResponse<int>.Success(7).Match(value => value * 2, _ => -1).ShouldBe(14);
        BaseResponse<int>.NotFound().Match(value => value * 2, _ => -1).ShouldBe(-1);
    }

    [Fact]
    public void WithContext_preserves_the_runtime_type_and_the_payload()
    {
        // The enricher relies on this: if `with` lost the derived type, serialization would
        // silently drop Data because System.Text.Json writes the declared type's properties.
        UserDto dto = new(Guid.CreateVersion7(), "Ada");
        BaseResponse response = BaseResponse<UserDto>.Success(dto);

        BaseResponse enriched = response.WithContext("corr-1", "trace-1");

        enriched.ShouldBeOfType<BaseResponse<UserDto>>();
        ((BaseResponse<UserDto>)enriched).Data.ShouldBe(dto);
        enriched.CorrelationId.ShouldBe("corr-1");
        enriched.TraceId.ShouldBe("trace-1");
    }

    [Fact]
    public void PagedResponse_derives_its_paging_metadata()
    {
        PagedResponse<UserDto> page = PagedResponse<UserDto>.Create(
            [new UserDto(Guid.CreateVersion7(), "Ada")],
            pageNumber: 2,
            pageSize: 10,
            totalCount: 25);

        page.TotalPages.ShouldBe(3);
        page.HasNext.ShouldBeTrue();
        page.HasPrevious.ShouldBeTrue();
        page.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void PagedResponse_reports_no_neighbours_for_a_single_page()
    {
        PagedResponse<UserDto> page = PagedResponse<UserDto>.Create([], 1, 20, 0);

        page.TotalPages.ShouldBe(0);
        page.HasNext.ShouldBeFalse();
        page.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public void Duplicate_message_argument_keys_are_rejected()
    {
        Should.Throw<ArgumentException>(() =>
            ResponseArgs.From(("field", "a"), ("field", "b")));
    }
}
